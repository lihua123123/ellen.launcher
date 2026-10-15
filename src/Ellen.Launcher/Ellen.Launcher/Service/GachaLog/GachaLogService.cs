// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.GachaLog.Factory;
using Ellen.Launcher.Service.GachaLog.QueryProvider;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.ViewModel.GachaLog;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using Ellen.Launcher.Web.Hoyolab.Nap.Event.GachaRecord;
using Ellen.Launcher.Web.Response;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.GachaLog;

[Service(ServiceLifetime.Scoped, typeof(IGachaLogService))]
public sealed partial class GachaLogService : IGachaLogService
{
    private readonly IGachaStatisticsSlimFactory gachaStatisticsSlimFactory;
    private readonly IGachaStatisticsFactory gachaStatisticsFactory;
    private readonly IGachaLogRepository gachaLogRepository;
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<GachaLogService> logger;
    private readonly ITaskContext taskContext;

    private readonly AsyncLock archivesLock = new();
    private readonly Dictionary<GachaGameType, IAdvancedDbCollectionView<GachaArchive>> archivesByGame = [];

    [GeneratedConstructor]
    public partial GachaLogService(IServiceProvider serviceProvider);

    public async ValueTask<IAdvancedDbCollectionView<GachaArchive>> GetArchiveCollectionAsync(GachaGameType game)
    {
        using (await archivesLock.LockAsync().ConfigureAwait(false))
        {
            if (!archivesByGame.TryGetValue(game, out IAdvancedDbCollectionView<GachaArchive>? archives))
            {
                archives = gachaLogRepository.GetGachaArchiveCollection(game).ToAdvancedDbCollectionView(serviceProvider);
                archivesByGame[game] = archives;
            }

            return archives;
        }
    }

    public async ValueTask<IAdvancedDbCollectionView<GachaArchive>> ReloadArchiveCollectionAsync(GachaGameType game)
    {
        using (await archivesLock.LockAsync().ConfigureAwait(false))
        {
            // GetGachaArchiveCollection() always runs a fresh AsNoTracking query, so rebuilding the
            // view is enough to pick up archives that were inserted through another DbContext scope.
            IAdvancedDbCollectionView<GachaArchive> archives = gachaLogRepository.GetGachaArchiveCollection(game).ToAdvancedDbCollectionView(serviceProvider);
            archivesByGame[game] = archives;
            return archives;
        }
    }

    public async ValueTask<GachaStatistics> GetStatisticsAsync(GachaLogServiceMetadataContext context, GachaArchive archive)
    {
        using (ValueStopwatch.MeasureExecution(logger))
        {
            ImmutableArray<GachaItem> items = gachaLogRepository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId);
            return await gachaStatisticsFactory.CreateAsync(context, items, default!).ConfigureAwait(false);
        }
    }

    public async ValueTask<GachaStatistics> GetBeyondStatisticsAsync(GachaLogServiceMetadataContext context, GachaArchive archive)
    {
        using (ValueStopwatch.MeasureExecution(logger))
        {
            ImmutableArray<BeyondGachaItem> beyondItems = gachaLogRepository.GetBeyondGachaItemImmutableArrayByArchiveId(archive.InnerId);
            
            return await gachaStatisticsFactory.CreateAsync(context, default!, beyondItems).ConfigureAwait(false);
        }
    }

    public async ValueTask<ImmutableArray<GachaStatisticsSlim>> GetStatisticsSlimImmutableArrayAsync(GachaLogServiceMetadataContext context, CancellationToken token = default)
    {
        ImmutableArray<GachaStatisticsSlim>.Builder statistics = ImmutableArray.CreateBuilder<GachaStatisticsSlim>();
        foreach (GachaArchive archive in await GetArchiveCollectionAsync(GachaGameType.Genshin).ConfigureAwait(false))
        {
            ImmutableArray<GachaItem> items = gachaLogRepository.GetGachaItemImmutableArrayByArchiveId(archive.InnerId);
            GachaStatisticsSlim slim = await gachaStatisticsSlimFactory.CreateAsync(context, items, archive.Uid).ConfigureAwait(false);
            statistics.Add(slim);
        }

        return statistics.ToImmutable();
    }

    public async ValueTask<bool> RefreshGachaLogAsync(GachaLogServiceMetadataContext context, GachaLogQuery query, RefreshStrategyKind kind, IProgress<GachaLogFetchStatus> progress, CancellationToken token)
    {
        bool isLazy = kind switch
        {
            RefreshStrategyKind.AggressiveMerge => false,
            RefreshStrategyKind.LazyMerge => true,
            _ => throw EllenException.NotSupported(),
        };

        (bool authkeyValid, GachaArchive? target) = await FetchGachaLogsAsync(context, query, isLazy, progress, token).ConfigureAwait(false);

        if (target is not null)
        {
            IAdvancedDbCollectionView<GachaArchive> localArchives = await GetArchiveCollectionAsync(target.Game).ConfigureAwait(false);
            await taskContext.SwitchToMainThreadAsync();
            localArchives.MoveCurrentTo(target);
        }

        return authkeyValid;
    }

    public async ValueTask<bool> RefreshBeyondGachaLogAsync(GachaLogServiceMetadataContext context, GachaLogQuery query, RefreshStrategyKind kind, IProgress<GachaLogFetchStatus> progress, CancellationToken token)
    {
        bool isLazy = kind switch
        {
            RefreshStrategyKind.AggressiveMerge => false,
            RefreshStrategyKind.LazyMerge => true,
            _ => throw EllenException.NotSupported(),
        };

        (bool authkeyValid, GachaArchive? target) = await FetchBeyondGachaLogsAsync(context, query, isLazy, progress, token).ConfigureAwait(false);

        if (target is not null)
        {
            IAdvancedDbCollectionView<GachaArchive> localArchives = await GetArchiveCollectionAsync(target.Game).ConfigureAwait(false);
            await taskContext.SwitchToMainThreadAsync();
            localArchives.MoveCurrentTo(target);
        }

        return authkeyValid;
    }

    public async ValueTask RemoveArchiveAsync(GachaArchive archive)
    {
        // Sync database
        await taskContext.SwitchToBackgroundAsync();
        gachaLogRepository.RemoveGachaArchiveById(archive.InnerId);

        // Sync cache
        IAdvancedDbCollectionView<GachaArchive> localArchives = await GetArchiveCollectionAsync(archive.Game).ConfigureAwait(false);
        await taskContext.SwitchToMainThreadAsync();
        localArchives.Remove(archive);
        localArchives.MoveCurrentToFirst();
    }

    public async ValueTask<GachaArchive> EnsureArchiveInCollectionAsync(Guid archiveId, CancellationToken token = default)
    {
        GachaArchive? newArchive = gachaLogRepository.GetGachaArchiveById(archiveId);
        ArgumentNullException.ThrowIfNull(newArchive);

        IAdvancedDbCollectionView<GachaArchive> localArchives = await GetArchiveCollectionAsync(newArchive.Game).ConfigureAwait(false);
        if (localArchives.Source.SingleOrDefault(a => a.InnerId == archiveId) is { } archive)
        {
            return archive;
        }

        await taskContext.SwitchToMainThreadAsync();
        localArchives.Add(newArchive);
        return newArchive;
    }

    private async ValueTask<ValueResult<bool, GachaArchive?>> FetchGachaLogsAsync(GachaLogServiceMetadataContext context, GachaLogQuery query, bool isLazy, IProgress<GachaLogFetchStatus> progress, CancellationToken token)
    {
        GachaGameType game = query.Game;
        IAdvancedDbCollectionView<GachaArchive> localArchives = await GetArchiveCollectionAsync(game).ConfigureAwait(false);
        GachaLogFetchContext fetchContext = new(gachaLogRepository, context, isLazy, game);

        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            GachaInfoClient gachaInfoClient = scope.ServiceProvider.GetRequiredService<GachaInfoClient>();
            ZZZGachaInfoClient zzzGachaInfoClient = scope.ServiceProvider.GetRequiredService<ZZZGachaInfoClient>();

            foreach (GachaType configType in game is GachaGameType.ZZZ ? ZZZGachaLog.QueryTypes : GachaLog.QueryTypes)
            {
                fetchContext.ResetType(configType, query);

                do
                {
                    ImmutableArray<GachaLogItem> items;

                    if (game is GachaGameType.ZZZ)
                    {
                        Response<ZZZGachaLogPage> response = await zzzGachaInfoClient
                            .GetGachaLogPageAsync(fetchContext.TypedQueryOptions, token)
                            .ConfigureAwait(false);

                        // 绝区零分支：显式弹通知暴露真实 retcode/message，便于定位
                        if (!ResponseValidator.TryValidate(response, serviceProvider, out ZZZGachaLogPage? zzzPage))
                        {
                            fetchContext.Report(progress, isAuthKeyTimeout: true);
                            break;
                        }

                        items = zzzPage.List;
                    }
                    else
                    {
                        Response<GachaLogPage> response = await gachaInfoClient
                            .GetGachaLogPageAsync(fetchContext.TypedQueryOptions, token)
                            .ConfigureAwait(false);

                        // Fast break fetching if authkey timeout
                        if (!ResponseValidator.TryValidateWithoutUINotification(response, out GachaLogPage? page))
                        {
                            fetchContext.Report(progress, isAuthKeyTimeout: true);
                            break;
                        }

                        items = page.List;
                    }

                    fetchContext.ResetCurrentPage();

                    foreach (GachaLogItem item in items)
                    {
                        fetchContext.EnsureArchiveAndEndId(item, localArchives, gachaLogRepository);

                        if (fetchContext.ShouldAddItem(item))
                        {
                            fetchContext.AddItem(item);
                        }
                        else
                        {
                            fetchContext.CompleteCurrentTypeAdding();
                            break;
                        }
                    }

                    fetchContext.Report(progress);
                    await Task.Delay(Random.Shared.Next(1000, 2000), token).ConfigureAwait(false);

                    if (fetchContext.HasReachCurrentTypeEnd(items))
                    {
                        // Exit current type fetch loop
                        break;
                    }

                    // 绝区零用 page 翻页（与 zzz-signal-search-export 一致）
                    if (game is GachaGameType.ZZZ)
                    {
                        fetchContext.TypedQueryOptions.Page++;
                    }
                }
                while (true);

                // Fast break query type loop if authkey timeout, skip saving items
                if (fetchContext.Status.AuthKeyTimeout)
                {
                    break;
                }

                // Save items for each queryType
                token.ThrowIfCancellationRequested();
                fetchContext.SaveItems();

                // Delay between query types
                await Task.Delay(Random.Shared.Next(1000, 2000), token).ConfigureAwait(false);
            }
        }

        // 绝区零：所有频段都没有返回任何记录、且没有触发 authkey 失效时，明确提示，避免「静默无结果」
        if (game is GachaGameType.ZZZ && fetchContext.TargetArchive is null && !fetchContext.Status.AuthKeyTimeout)
        {
            serviceProvider.GetRequiredService<IMessenger>()
                .Send(InfoBarMessage.Warning(SH.ServiceGachaLogZZZNoRecordFetched));
        }

        return new(!fetchContext.Status.AuthKeyTimeout, fetchContext.TargetArchive);
    }

    private async ValueTask<ValueResult<bool, GachaArchive?>> FetchBeyondGachaLogsAsync(GachaLogServiceMetadataContext context, GachaLogQuery query, bool isLazy, IProgress<GachaLogFetchStatus> progress, CancellationToken token)
    {
        IAdvancedDbCollectionView<GachaArchive> localArchives = await GetArchiveCollectionAsync(GachaGameType.Genshin).ConfigureAwait(false);
        BeyondGachaLogFetchContext fetchContext = new(gachaLogRepository, context, isLazy);

        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            GachaInfoClient gachaInfoClient = scope.ServiceProvider.GetRequiredService<GachaInfoClient>();

            foreach (GachaType configType in BeyondGachaLog.QueryTypes)
            {
                fetchContext.ResetType(configType, query);

                do
                {
                    Response<BeyondGachaLogPage> response = await gachaInfoClient
                        .GetBeyondGachaLogPageAsync(fetchContext.TypedQueryOptions, token)
                        .ConfigureAwait(false);

                    // Fast break fetching if authkey timeout
                    if (!ResponseValidator.TryValidateWithoutUINotification(response, out BeyondGachaLogPage? page))
                    {
                        fetchContext.Report(progress, isAuthKeyTimeout: true);
                        break;
                    }

                    fetchContext.ResetCurrentPage();
                    ImmutableArray<BeyondGachaLogItem> items = page.List;

                    foreach (BeyondGachaLogItem item in items)
                    {
                        fetchContext.EnsureArchiveAndEndId(item, localArchives, gachaLogRepository);

                        if (fetchContext.ShouldAddItem(item))
                        {
                            fetchContext.AddItem(item);
                        }
                        else
                        {
                            fetchContext.CompleteCurrentTypeAdding();
                            break;
                        }
                    }

                    fetchContext.Report(progress);
                    await Task.Delay(Random.Shared.Next(1000, 2000), token).ConfigureAwait(false);

                    if (fetchContext.HasReachCurrentTypeEnd(items))
                    {
                        // Exit current type fetch loop
                        break;
                    }
                }
                while (true);

                // Fast break query type loop if authkey timeout, skip saving items
                if (fetchContext.Status.AuthKeyTimeout)
                {
                    break;
                }

                // Save items for each queryType
                token.ThrowIfCancellationRequested();
                fetchContext.SaveItems();

                // Delay between query types
                await Task.Delay(Random.Shared.Next(1000, 2000), token).ConfigureAwait(false);
            }
        }

        return new(!fetchContext.Status.AuthKeyTimeout, fetchContext.TargetArchive);
    }
}
