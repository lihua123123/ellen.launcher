// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Database;
using Ellen.Launcher.Service.Abstraction;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.GachaLog;

[Service(ServiceLifetime.Singleton, typeof(IGachaLogRepository))]
public sealed partial class GachaLogRepository : IGachaLogRepository
{
    [GeneratedConstructor]
    public partial GachaLogRepository(IServiceProvider serviceProvider);

    public partial IServiceProvider ServiceProvider { get; }

    public ObservableCollection<GachaArchive> GetGachaArchiveCollection()
    {
        return this.ObservableCollection<GachaArchive>();
    }

    public ImmutableArray<GachaItem> GetGachaItemImmutableArrayByArchiveId(Guid archiveId)
    {
        return this.ImmutableArray<GachaItem, GachaItem>(query => query.Where(i => i.ArchiveId == archiveId).OrderBy(i => i.Id));
    }

    public void RemoveGachaArchiveById(Guid archiveId)
    {
        this.DeleteByInnerId<GachaArchive>(archiveId);
    }

    public long GetNewestGachaItemIdByArchiveIdAndQueryType(Guid archiveId, GachaType queryType)
    {
        GachaItem? item = this.Query<GachaItem, GachaItem?>(query => query
            .Where(i => i.ArchiveId == archiveId && i.QueryType == queryType)
            .OrderByDescending(i => i.Id)
            .FirstOrDefault());

        return item?.Id ?? 0L;
    }

    public long GetOldestGachaItemIdByArchiveId(Guid archiveId)
    {
        GachaItem? item = this.Query<GachaItem, GachaItem?>(query => query
            .Where(i => i.ArchiveId == archiveId)
            .OrderBy(i => i.Id)
            .FirstOrDefault());

        return item?.Id ?? long.MaxValue;
    }

    public long GetOldestGachaItemIdByArchiveIdAndQueryType(Guid archiveId, GachaType queryType)
    {
        GachaItem? item = this.Query<GachaItem, GachaItem?>(query => query
            .Where(i => i.ArchiveId == archiveId && i.QueryType == queryType)
            .OrderBy(i => i.Id)
            .FirstOrDefault());

        return item?.Id ?? long.MaxValue;
    }

    public void AddGachaArchive(GachaArchive archive)
    {
        using (IServiceScope scope = ServiceProvider.CreateScope())
        {
            AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            appDbContext.GachaArchives.AddAndSave(archive);
        }
    }

    public GachaArchive? GetGachaArchiveById(Guid archiveId)
    {
        return this.SingleOrDefault<GachaArchive>(a => a.InnerId == archiveId);
    }

    public GachaArchive? GetGachaArchiveByUid(string uid)
    {
        return this.SingleOrDefault<GachaArchive>(a => a.Uid == uid);
    }

    public void AddGachaItemRange(IEnumerable<GachaItem> items)
    {
        this.AddRange(items);
    }

    public void RemoveGachaItemRangeByArchiveIdAndQueryTypeNewerThanEndId(Guid archiveId, GachaType queryType, long endId)
    {
        this.Delete<GachaItem>(i => i.ArchiveId == archiveId && i.QueryType == queryType && i.Id >= endId);
    }

    public ImmutableArray<string> GetGachaArchiveUidImmutableArray()
    {
        return this.ImmutableArray<GachaArchive, string>(query => query.Select(archive => archive.Uid));
    }

    public void AddBeyondGachaItemRange(IEnumerable<BeyondGachaItem> items)
    {
        this.AddRange(items);
    }

    public void RemoveBeyondGachaItemRangeByArchiveIdAndGachaTypeNewerThanEndId(Guid archiveId, GachaType queryType, long endId)
    {
        this.Delete<BeyondGachaItem>(i => i.ArchiveId == archiveId && i.QueryType == queryType && i.Id >= endId);
    }

    public ImmutableArray<BeyondGachaItem> GetBeyondGachaItemImmutableArrayByArchiveId(Guid archiveId)
    {
        return this.ImmutableArray<BeyondGachaItem, BeyondGachaItem>(query => query.Where(i => i.ArchiveId == archiveId).OrderBy(i => i.Id));
    }

    public long GetNewestBeyondGachaItemIdByArchiveIdAndGachaType(Guid archiveId, GachaType queryType)
    {
        BeyondGachaItem? item = this.Query<BeyondGachaItem, BeyondGachaItem?>(query => query
            .Where(i => i.ArchiveId == archiveId && i.QueryType == queryType)
            .OrderByDescending(i => i.Id)
            .FirstOrDefault());

        return item?.Id ?? 0L;
    }

    public long GetOldestBeyondGachaItemIdByArchiveIdAndGachaType(Guid archiveId, GachaType queryType)
    {
        BeyondGachaItem? item = this.Query<BeyondGachaItem, BeyondGachaItem?>(query => query
            .Where(i => i.ArchiveId == archiveId && i.QueryType == queryType)
            .OrderBy(i => i.Id)
            .FirstOrDefault());

        return item?.Id ?? long.MaxValue;
    }
}
