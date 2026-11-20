// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.UIGF;

public abstract partial class AbstractUIGF40ExportService : IUIGFExportService
{
    protected readonly JsonSerializerOptions jsonOptions;
    protected readonly IServiceProvider serviceProvider;
    protected readonly ITaskContext taskContext;
    protected readonly IMetadataService metadataService;

    [GeneratedConstructor]
    public partial AbstractUIGF40ExportService(IServiceProvider serviceProvider);

    protected abstract string Version { get; }

    public virtual async ValueTask ExportAsync(UIGFExportOptions exportOptions, CancellationToken token = default)
    {
        await taskContext.SwitchToBackgroundAsync();

        UIGFInfo info = new()
        {
            ExportApp = "Ellen Launcher",
            ExportAppVersion = $"{EllenRuntime.Version}",
            ExportTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
            Version = Version,
        };
        UIGF4 uigf = CreateDocument(info);
        IGachaLogRepository repository = serviceProvider.GetRequiredService<IGachaLogRepository>();
        GachaLogServiceMetadataContext? metadata = null;
        ImmutableArray<UIGFEntry<Hk4eItem>>.Builder hk4e = ImmutableArray.CreateBuilder<UIGFEntry<Hk4eItem>>();
        ImmutableArray<UIGFEntry<NapItem>>.Builder nap = ImmutableArray.CreateBuilder<UIGFEntry<NapItem>>();

        foreach ((GachaGameType game, ulong uid) in exportOptions.Accounts.Distinct())
        {
            token.ThrowIfCancellationRequested();
            GachaArchive? archive = repository.GetGachaArchiveByUidAndGame($"{uid}", game);
            ArgumentNullException.ThrowIfNull(archive);
            ImmutableArray<GachaItem> items = repository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId);

            if (game is GachaGameType.Genshin)
            {
                metadata ??= await metadataService.GetContextAsync<GachaLogServiceMetadataContext>(token).ConfigureAwait(false);
                if (!items.IsDefaultOrEmpty)
                {
                    int timezone = items[0].Time.Offset.Hours;
                    hk4e.Add(new()
                    {
                        Uid = uid,
                        TimeZone = timezone,
                        List = items.SelectAsArray(item => ExportGenshinItem(item, metadata, timezone)),
                    });
                }

                ExportAdditionalGenshinData(uigf, repository, archive, uid, metadata);
            }
            else if (game is GachaGameType.ZZZ)
            {
                ImmutableArray<GachaItem> supportedItems = [.. items.Where(item => ZZZGachaLog.QueryTypes.Contains(item.GachaType))];
                if (!supportedItems.IsEmpty)
                {
                    int timezone = supportedItems[0].Time.Offset.Hours;
                    nap.Add(new()
                    {
                        Uid = uid,
                        TimeZone = timezone,
                        List = supportedItems.SelectAsArray(item => NapItem.From(item, timezone)),
                    });
                }
            }
        }

        if (hk4e.Count > 0)
        {
            uigf.Hk4e = hk4e.ToImmutable();
        }
        if (nap.Count > 0)
        {
            uigf.Nap = nap.ToImmutable();
        }
        if (hk4e.Count == 0 && nap.Count == 0 && (uigf is not UIGF42 uigf42 || uigf42.Hk4eUgc.IsDefaultOrEmpty))
        {
            throw new InvalidOperationException(SH.ViewModelUIGFImportNoHk4eEntry);
        }

        using FileStream stream = File.Create(exportOptions.FilePath);
        await JsonSerializer.SerializeAsync(stream, uigf, uigf.GetType(), jsonOptions, token).ConfigureAwait(false);
    }

    protected virtual UIGF4 CreateDocument(UIGFInfo info) => new() { Info = info };

    protected virtual void ExportAdditionalGenshinData(UIGF4 uigf, IGachaLogRepository repository, GachaArchive archive, ulong uid, GachaLogServiceMetadataContext metadata)
    {
    }

    internal static Hk4eItem ExportGenshinItem(GachaItem item, GachaLogServiceMetadataContext metadata, int timezone)
    {
        INameQualityAccess? nameQuality = null;
        string? itemType = null;
        switch (item.ItemId.StringLength)
        {
            case 8U:
                if (metadata.IdAvatarMap.TryGetValue(item.ItemId, out var avatar))
                {
                    nameQuality = avatar;
                }
                itemType = SH.ModelInterchangeUIGFItemTypeAvatar;
                break;
            case 5U:
                if (metadata.IdWeaponMap.TryGetValue(item.ItemId, out var weapon))
                {
                    nameQuality = weapon;
                }
                itemType = SH.ModelInterchangeUIGFItemTypeWeapon;
                break;
        }

        return Hk4eItem.From(item, nameQuality?.Name, itemType, nameQuality is null ? null : ((int)nameQuality.Quality).ToString(), timezone);
    }
}
