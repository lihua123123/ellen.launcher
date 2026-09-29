// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;

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
        ImportGachaArchives(uigf.Hk4eUgc, importOptions.GachaArchiveUids);
    }

    private void ImportGachaArchives(ImmutableArray<UIGFEntry<Hk4eUGCItem>> entries, HashSet<uint> uids)
    {
        if (entries.IsDefaultOrEmpty || uids is null or { Count: 0 })
        {
            return;
        }

        IGachaLogRepository gachaLogRepository = serviceProvider.GetRequiredService<IGachaLogRepository>();

        foreach (ref readonly UIGFEntry<Hk4eUGCItem> entry in entries.AsSpan())
        {
            if (!uids.Contains(entry.Uid))
            {
                continue;
            }

            foreach (Hk4eUGCItem item in entry.List)
            {
                if (item.ItemId is 0)
                {
                    throw EllenException.Throw(SH.FormatServiceUIGFImportInvalidItem(item.Id));
                }
            }

            GachaArchive? archive = gachaLogRepository.GetGachaArchiveByUid($"{entry.Uid}");

            if (archive is null)
            {
                archive = GachaArchive.Create($"{entry.Uid}");
                gachaLogRepository.AddGachaArchive(archive);
            }

            Guid archiveId = archive.InnerId;

            List<BeyondGachaItem> fullItems = [];
            int timeZone = entry.TimeZone;
            foreach (GachaType queryType in BeyondGachaLog.QueryTypes)
            {
                long trimId = gachaLogRepository.GetOldestBeyondGachaItemIdByArchiveIdAndGachaType(archiveId, queryType);
                List<BeyondGachaItem> currentTypedList = entry.List
                    .Where(item => item.GachaType.ToQueryType() == queryType && item.Id < trimId)
                    .OrderByDescending(item => item.Id)
                    .Select(item => BeyondGachaItem.From(archiveId, item, timeZone))
                    .ToList();

                fullItems.AddRange(currentTypedList);
            }

            gachaLogRepository.AddBeyondGachaItemRange(fullItems);
        }
    }
}
