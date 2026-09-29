// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Service.GachaLog;
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
        ImportGachaArchives(importOptions.UIGF.Hk4e, importOptions.GachaArchiveUids);
    }

    public bool Parse(string json, out Model.InterChange.GachaLog.UIGF4? uigf)
    {
        if (uigfService.Parse(json, out uigf) && uigf is not null)
        {
            return true;
        }

        return false;
    }

    private void ImportGachaArchives(ImmutableArray<UIGFEntry<Hk4eItem>> entries, HashSet<uint> uids)
    {
        if (entries.IsDefaultOrEmpty || uids is null or { Count: 0 })
        {
            return;
        }

        IGachaLogRepository gachaLogRepository = serviceProvider.GetRequiredService<IGachaLogRepository>();

        foreach (ref readonly UIGFEntry<Hk4eItem> entry in entries.AsSpan())
        {
            if (!uids.Contains(entry.Uid))
            {
                continue;
            }

            foreach (Hk4eItem item in entry.List)
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

            List<GachaItem> fullItems = [];
            int timeZone = entry.TimeZone;
            foreach (GachaType queryType in GachaLog.GachaLog.QueryTypes)
            {
                long trimId = gachaLogRepository.GetOldestGachaItemIdByArchiveIdAndQueryType(archiveId, queryType);
                List<GachaItem> currentTypedList = entry.List
                    .Where(item => item.UIGFGachaType == (UIGFGachaType)queryType && item.Id < trimId)
                    .OrderByDescending(item => item.Id)
                    .Select(item => GachaItem.From(archiveId, item, timeZone))
                    .ToList();

                fullItems.AddRange(currentTypedList);
            }

            gachaLogRepository.AddGachaItemRange(fullItems);
        }
    }
}