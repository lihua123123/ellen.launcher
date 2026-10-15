// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.Abstraction;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.GachaLog;

public interface IGachaLogRepository : IRepository<GachaArchive>, IRepository<GachaItem>, IRepository<BeyondGachaItem>
{
    void AddGachaArchive(GachaArchive archive);

    void AddGachaItemRange(IEnumerable<GachaItem> items);

    void RemoveGachaArchiveById(Guid archiveId);

    void RemoveGachaItemRangeByArchiveIdAndQueryTypeNewerThanEndId(Guid archiveId, GachaType queryType, long endId);

    GachaArchive? GetGachaArchiveById(Guid archiveId);

    GachaArchive? GetGachaArchiveByUid(string uid);

    GachaArchive? GetGachaArchiveByUidAndGame(string uid, GachaGameType game);

    ImmutableArray<string> GetGachaArchiveUidImmutableArray();

    ObservableCollection<GachaArchive> GetGachaArchiveCollection();

    ObservableCollection<GachaArchive> GetGachaArchiveCollection(GachaGameType game);

    ImmutableArray<GachaItem> GetGachaItemImmutableArrayByArchiveId(Guid archiveId);

    long GetNewestGachaItemIdByArchiveIdAndQueryType(Guid archiveId, GachaType queryType);

    long GetOldestGachaItemIdByArchiveId(Guid archiveId);

    long GetOldestGachaItemIdByArchiveIdAndQueryType(Guid archiveId, GachaType queryType);

    // Beyond Gacha (UGC) methods
    void AddBeyondGachaItemRange(IEnumerable<BeyondGachaItem> items);

    void RemoveBeyondGachaItemRangeByArchiveIdAndGachaTypeNewerThanEndId(Guid archiveId, GachaType GachaType, long endId);

    ImmutableArray<BeyondGachaItem> GetBeyondGachaItemImmutableArrayByArchiveId(Guid archiveId);

    long GetNewestBeyondGachaItemIdByArchiveIdAndGachaType(Guid archiveId, GachaType GachaType);

    long GetOldestBeyondGachaItemIdByArchiveIdAndGachaType(Guid archiveId, GachaType GachaType);
}
