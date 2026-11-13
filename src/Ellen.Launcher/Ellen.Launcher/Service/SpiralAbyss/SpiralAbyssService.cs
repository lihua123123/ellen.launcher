// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Domain.GameRecord;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Metadata.Tower;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.ViewModel.SpiralAbyss;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Response;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.SpiralAbyss;

[Service(ServiceLifetime.Scoped, typeof(ISpiralAbyssService))]
public sealed partial class SpiralAbyssService : ISpiralAbyssService
{
    private readonly ISpiralAbyssRepository spiralAbyssRepository;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ITaskContext taskContext;
    private readonly ILogger<SpiralAbyssService> logger;

    private readonly ConcurrentDictionary<PlayerUid, ObservableCollection<SpiralAbyssView>> spiralAbyssCollectionCache = [];
    private readonly AsyncLock collectionLock = new();

    // 国服 game_record 的期次时间按 UTC+8 展示
    private static readonly TimeSpan ServerTimeOffset = TimeSpan.FromHours(8);

    [GeneratedConstructor]
    public partial SpiralAbyssService(IServiceProvider serviceProvider);

    public async ValueTask<ObservableCollection<SpiralAbyssView>> GetSpiralAbyssViewCollectionAsync(SpiralAbyssMetadataContext context, UserAndUid userAndUid)
    {
        using (await collectionLock.LockAsync().ConfigureAwait(false))
        {
            if (spiralAbyssCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<SpiralAbyssView>? collection))
            {
                return collection;
            }

            await taskContext.SwitchToBackgroundAsync();
            FrozenDictionary<uint, SpiralAbyssEntry> entryMap = spiralAbyssRepository.GetSpiralAbyssEntryMapByUid(userAndUid.Uid.Value);

            // 随包的 TowerSchedule.json 是静态白名单。库里已经存过的期次可能比表新，
            // 先用记录自带的官方起止时间补登记，否则 SpiralAbyssView 构造时按 Id 索引会
            // KeyNotFoundException，整条历史记录上不了屏。
            foreach (SpiralAbyssEntry entry in entryMap.Values)
            {
                if (entry.SpiralAbyss is { } recorded)
                {
                    EnsureScheduleRegistered(context, recorded.ScheduleId, recorded.StartTime, recorded.EndTime);
                }
            }

            ObservableCollection<SpiralAbyssView> result = OrderedSchedules(context)
                .Select(schedule => SpiralAbyssView.From(entryMap.GetValueOrDefault(schedule.Id), schedule, context))
                .ToObservableCollection();

            spiralAbyssCollectionCache.TryAdd(userAndUid.Uid, result);
            return result;
        }
    }

    public async ValueTask RefreshSpiralAbyssAsync(SpiralAbyssMetadataContext context, UserAndUid userAndUid)
    {
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            IMessenger messenger = scope.ServiceProvider.GetRequiredService<IMessenger>();
            IOverseaSupportFactory<IGameRecordClient> gameRecordClientFactory = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<IGameRecordClient>>();
            IGameRecordClient gameRecordClient = gameRecordClientFactory.Create(userAndUid.IsOversea);

            // request the index first
            Response<PlayerInfo> infoResponse = await gameRecordClient
                .GetPlayerInfoAsync(userAndUid)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(infoResponse, messenger))
            {
                return;
            }

            await PrivateRefreshSpiralAbyssAsync(messenger, gameRecordClient, context, userAndUid, ScheduleType.Last).ConfigureAwait(false);
            await PrivateRefreshSpiralAbyssAsync(messenger, gameRecordClient, context, userAndUid, ScheduleType.Current).ConfigureAwait(false);
        }
    }

    private async ValueTask PrivateRefreshSpiralAbyssAsync(IMessenger messenger, IGameRecordClient gameRecordClient, SpiralAbyssMetadataContext context, UserAndUid userAndUid, ScheduleType schedule)
    {
        if (!spiralAbyssCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<SpiralAbyssView>? spiralAbysses))
        {
            return;
        }

        Response<Web.Hoyolab.Takumi.GameRecord.SpiralAbyss.SpiralAbyss> response = await gameRecordClient
            .GetSpiralAbyssAsync(userAndUid, schedule)
            .ConfigureAwait(false);

        if (!ResponseValidator.TryValidate(response, messenger, out Web.Hoyolab.Takumi.GameRecord.SpiralAbyss.SpiralAbyss? webSpiralAbyss))
        {
            return;
        }

        // 表外的新期：先按接口的官方起止时间登记，再补一张占位卡片。
        // 旧实现在这里 FirstIndexOf 返回 -1 直接 return，请求成功也既不落库也不上屏。
        EnsureScheduleRegistered(context, webSpiralAbyss.ScheduleId, webSpiralAbyss.StartTime, webSpiralAbyss.EndTime);
        await EnsureScheduleViewAsync(context, userAndUid, webSpiralAbyss.ScheduleId).ConfigureAwait(false);

        int index = spiralAbysses.FirstIndexOf(s => s.ScheduleId == webSpiralAbyss.ScheduleId);
        if (index < 0)
        {
            return;
        }

        await taskContext.SwitchToBackgroundAsync();
        SpiralAbyssView view = spiralAbysses[index];

        SpiralAbyssEntry targetEntry;
        if (view.Entity is not null)
        {
            view.Entity.SpiralAbyss = webSpiralAbyss;
            spiralAbyssRepository.UpdateSpiralAbyssEntry(view.Entity);
            targetEntry = view.Entity;
        }
        else
        {
            SpiralAbyssEntry newEntry = SpiralAbyssEntry.Create(userAndUid.Uid.Value, webSpiralAbyss);
            spiralAbyssRepository.AddSpiralAbyssEntry(newEntry);
            targetEntry = newEntry;
        }

        await taskContext.SwitchToMainThreadAsync();
        spiralAbysses.RemoveAt(index);
        spiralAbysses.Insert(index, SpiralAbyssView.From(targetEntry, context));
    }

    /// <summary>
    /// 保证列表里存在指定期次的卡片（按开始时间倒序插入）。没有个人记录时用空实体卡片占位。
    /// </summary>
    private async ValueTask EnsureScheduleViewAsync(SpiralAbyssMetadataContext context, UserAndUid userAndUid, uint scheduleId)
    {
        if (!spiralAbyssCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<SpiralAbyssView>? spiralAbysses))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();

        if (spiralAbysses.FirstIndexOf(s => s.ScheduleId == scheduleId) >= 0)
        {
            return;
        }

        if (!context.IdTowerScheduleMap.TryGetValue(scheduleId, out TowerSchedule? meta))
        {
            return;
        }

        List<SchedulePeriod> positions = [.. spiralAbysses.Select(static view => new SchedulePeriod(view.ScheduleId, view.ScheduleBegin))];
        int insertAt = ScheduleTimeline.InsertIndex(positions, new SchedulePeriod(scheduleId, meta.Open));

        spiralAbysses.Insert(insertAt, SpiralAbyssView.From(null, meta, context));
    }

    /// <summary>
    /// 静态期次表缺该期时，用接口返回的官方起止时间登记，不自行编造期次内容。
    /// </summary>
    private void EnsureScheduleRegistered(SpiralAbyssMetadataContext context, uint scheduleId, long startTime, long endTime)
    {
        ImmutableDictionary<TowerScheduleId, TowerSchedule>? map = context.IdTowerScheduleMap;
        if (map is null || !ScheduleTimeline.IsValidId(scheduleId) || !ScheduleTimeline.NeedsRegistration(map.Keys.Select(static key => (uint)key), scheduleId))
        {
            // 0 不是有效期次：接口在没有数据时可能回 0，登记进列表会变成一张「第 0 期」的空卡片
            return;
        }

        // 深境没有期次名，也没有随表下发的祝福 / 楼层：这些字段留空，
        // 界面按「第 N 期 + 官方起止时间」展示，不编造文案。
        context.IdTowerScheduleMap = map.Add(scheduleId, new TowerSchedule
        {
            Id = scheduleId,
            FloorIds = [],
            Open = ToServerTime(startTime),
            Close = ToServerTime(endTime),
            BuffName = string.Empty,
            Descriptions = [],
            Icon = string.Empty,
        });

        logger.LogInformation("深境补登记表外期次 {ScheduleId}（{Begin:o} - {End:o}）", scheduleId, ToServerTime(startTime), ToServerTime(endTime));
    }

    private static List<TowerSchedule> OrderedSchedules(SpiralAbyssMetadataContext context)
    {
        return
        [
            .. ScheduleTimeline
                .SortDescending(context.IdTowerScheduleMap.Select(static pair => new SchedulePeriod(pair.Key, pair.Value.Open)))
                .Select(period => context.IdTowerScheduleMap[period.Id])
        ];
    }

    private static DateTimeOffset ToServerTime(long unixSeconds)
    {
        // 缺失（0 / 负数）时保留 default，由视图显示「时间未知」，不编一个看起来正常的时间
        return unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToOffset(ServerTimeOffset) : default;
    }
}
