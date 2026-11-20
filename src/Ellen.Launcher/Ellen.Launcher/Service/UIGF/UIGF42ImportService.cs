// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

namespace Ellen.Launcher.Service.UIGF;

[Service(ServiceLifetime.Transient, typeof(IUIGFImportService), Key = UIGFVersion.UIGF42)]
public sealed partial class UIGF42ImportService : AbstractUIGF40ImportService
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UIGF42ImportService(IServiceProvider serviceProvider);

    public override async ValueTask ImportAsync(UIGFImportOptions importOptions, CancellationToken token = default)
    {
        await base.ImportAsync(importOptions, token).ConfigureAwait(false);
        await taskContext.SwitchToBackgroundAsync();
        UIGF42 uigf = (UIGF42)importOptions.UIGF;
        IGachaLogRepository repository = serviceProvider.GetRequiredService<IGachaLogRepository>();

        foreach (UIGFEntry<Hk4eUGCItem> entry in uigf.Hk4eUgc.IsDefault ? [] : uigf.Hk4eUgc)
        {
            token.ThrowIfCancellationRequested();
            if (entry.List.IsDefaultOrEmpty || !importOptions.Accounts.Contains((GachaGameType.Genshin, entry.Uid)))
            {
                continue;
            }

            GachaArchive? archive = repository.GetGachaArchiveByUidAndGame($"{entry.Uid}", GachaGameType.Genshin);
            HashSet<long> existingIds = archive is null ? [] : repository.GetBeyondGachaItemImmutableArrayByArchiveId(archive.InnerId).Select(item => item.Id).ToHashSet();
            List<BeyondGachaItem> items = [];
            foreach (Hk4eUGCItem item in entry.List)
            {
                if (!BeyondGachaLog.QueryTypes.Contains(item.GachaType.ToQueryType()) || !existingIds.Add(item.Id))
                {
                    continue;
                }

                if (item.ItemId is 0 || item.Id <= 0)
                {
                    throw EllenException.Throw(SH.FormatServiceUIGFImportInvalidItem(item.Id));
                }

                items.Add(BeyondGachaItem.From(default, item, entry.TimeZone));
            }

            if (items.Count == 0)
            {
                continue;
            }

            if (archive is null)
            {
                archive = GachaArchive.Create($"{entry.Uid}", GachaGameType.Genshin);
                repository.AddGachaArchive(archive);
            }

            foreach (BeyondGachaItem item in items)
            {
                item.ArchiveId = archive.InnerId;
            }

            repository.AddBeyondGachaItemRange(items);
        }
    }
}
