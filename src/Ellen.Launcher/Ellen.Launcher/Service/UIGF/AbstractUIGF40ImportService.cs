// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.UIGF;

public abstract partial class AbstractUIGF40ImportService : IUIGFImportService
{
    protected readonly IServiceProvider serviceProvider;
    protected readonly ITaskContext taskContext;
    protected readonly IUIGFService uigfService;

    [GeneratedConstructor]
    public partial AbstractUIGF40ImportService(IServiceProvider serviceProvider);

    public virtual async ValueTask ImportAsync(UIGFImportOptions importOptions, CancellationToken token = default)
    {
        await taskContext.SwitchToBackgroundAsync();

        IGachaLogRepository repository = serviceProvider.GetRequiredService<IGachaLogRepository>();
        GachaLogServiceMetadataContext? metadata = null;
        bool isLegacy = importOptions.UIGF.Info.Version.StartsWith("v2.", StringComparison.Ordinal);

        ImmutableArray<UIGFEntry<Hk4eItem>> hk4e = importOptions.UIGF.Hk4e;
        foreach (UIGFEntry<Hk4eItem> entry in hk4e.IsDefault ? [] : hk4e)
        {
            token.ThrowIfCancellationRequested();
            if (entry.List.IsDefaultOrEmpty || !importOptions.Accounts.Contains((GachaGameType.Genshin, entry.Uid)))
            {
                continue;
            }

            GachaArchive? archive = repository.GetGachaArchiveByUidAndGame($"{entry.Uid}", GachaGameType.Genshin);
            HashSet<long> existingIds = archive is null ? [] : repository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId).Select(item => item.Id).ToHashSet();
            List<GachaItem> items = [];
            foreach (Hk4eItem item in entry.List)
            {
                if (!existingIds.Add(item.Id))
                {
                    continue;
                }

                uint itemId = item.ItemId;
                if (itemId is 0 && isLegacy && item.Name is { Length: > 0 } name)
                {
                    metadata ??= await serviceProvider.GetRequiredService<IMetadataService>()
                        .GetContextAsync<GachaLogServiceMetadataContext>(token).ConfigureAwait(false);
                    var avatar = metadata.NameAvatarMap.GetValueOrDefault(name);
                    var weapon = metadata.NameWeaponMap.GetValueOrDefault(name);
                    itemId = (avatar, weapon) switch
                    {
                        (not null, null) => (uint)avatar.Id,
                        (null, not null) => (uint)weapon.Id,
                        _ => 0U,
                    };
                }

                if (itemId is 0 || item.Id <= 0 || item.GachaType.ToQueryType() != (GachaType)item.UIGFGachaType)
                {
                    throw EllenException.Throw(SH.FormatServiceUIGFImportInvalidItem(item.Id));
                }

                GachaItem entity = GachaItem.From(default, item, entry.TimeZone);
                entity.ItemId = itemId;
                items.Add(entity);
            }

            SaveNewItems(repository, archive, GachaGameType.Genshin, entry.Uid, items);
        }

        ImmutableArray<UIGFEntry<NapItem>> nap = importOptions.UIGF.Nap;
        foreach (UIGFEntry<NapItem> entry in nap.IsDefault ? [] : nap)
        {
            token.ThrowIfCancellationRequested();
            if (entry.List.IsDefaultOrEmpty || !importOptions.Accounts.Contains((GachaGameType.ZZZ, entry.Uid)))
            {
                continue;
            }

            GachaArchive? archive = repository.GetGachaArchiveByUidAndGame($"{entry.Uid}", GachaGameType.ZZZ);
            HashSet<long> existingIds = archive is null ? [] : repository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId).Select(item => item.Id).ToHashSet();
            List<GachaItem> items = [];
            foreach (NapItem item in entry.List)
            {
                if (!ZZZGachaLog.QueryTypes.Contains(item.GachaType) || !existingIds.Add(item.Id))
                {
                    continue;
                }

                if (item.Id <= 0)
                {
                    throw EllenException.Throw(SH.FormatServiceUIGFImportInvalidItem(item.Id));
                }

                items.Add(new()
                {
                    GachaType = item.GachaType,
                    QueryType = item.GachaType,
                    ItemId = item.ItemId,
                    Time = new(item.Time, TimeSpan.FromHours(entry.TimeZone)),
                    Id = item.Id,
                    Name = string.IsNullOrWhiteSpace(item.Name) ? null : item.Name,
                    Quality = (int)GachaItem.ZZZQualityToQualityType((QualityType)(item.RankType ?? 0)),
                });
            }

            SaveNewItems(repository, archive, GachaGameType.ZZZ, entry.Uid, items);
        }
    }

    public bool Parse(string json, out UIGF4? uigf)
    {
        return uigfService.Parse(json, out uigf);
    }

    private static void SaveNewItems(IGachaLogRepository repository, GachaArchive? archive, GachaGameType game, ulong uid, List<GachaItem> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        if (archive is null)
        {
            archive = GachaArchive.Create($"{uid}", game);
            repository.AddGachaArchive(archive);
        }

        foreach (GachaItem item in items)
        {
            item.ArchiveId = archive.InnerId;
        }

        repository.AddGachaItemRange(items);
    }
}
