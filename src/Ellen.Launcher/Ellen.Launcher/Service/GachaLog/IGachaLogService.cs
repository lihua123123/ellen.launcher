// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.GachaLog.QueryProvider;
using Ellen.Launcher.ViewModel.GachaLog;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.GachaLog;

public interface IGachaLogService
{
    ValueTask<GachaArchive> EnsureArchiveInCollectionAsync(Guid archiveId, CancellationToken token = default);

    ValueTask<GachaStatistics> GetStatisticsAsync(GachaLogServiceMetadataContext context, GachaArchive archive);

    ValueTask<GachaStatistics> GetBeyondStatisticsAsync(GachaLogServiceMetadataContext context, GachaArchive archive);

    ValueTask<ImmutableArray<GachaStatisticsSlim>> GetStatisticsSlimImmutableArrayAsync(GachaLogServiceMetadataContext context, CancellationToken token = default);

    ValueTask<bool> RefreshGachaLogAsync(GachaLogServiceMetadataContext context, GachaLogQuery query, RefreshStrategyKind strategy, IProgress<GachaLogFetchStatus> progress, CancellationToken token);

    ValueTask<bool> RefreshBeyondGachaLogAsync(GachaLogServiceMetadataContext context, GachaLogQuery query, RefreshStrategyKind strategy, IProgress<GachaLogFetchStatus> progress, CancellationToken token);

    ValueTask RemoveArchiveAsync(GachaArchive archive);

    ValueTask<IAdvancedDbCollectionView<GachaArchive>> GetArchiveCollectionAsync();

    /// <summary>
    /// 重新从数据库查询存档集合。
    /// <see cref="GetArchiveCollectionAsync"/> 返回的是一次性查询的快照并会被缓存，
    /// 当存档由本服务以外的作用域插入时（例如 UIGF 导入、刷新），必须调用本方法才能看到新行。
    /// </summary>
    ValueTask<IAdvancedDbCollectionView<GachaArchive>> ReloadArchiveCollectionAsync();
}
