// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Domain.GameRecord;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.ViewModel.HardChallenge;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;
using Ellen.Launcher.Web.Response;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using GameRecordDateTime = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.DateTime;
using MetadataHardChallengeSchedule = Ellen.Launcher.Model.Metadata.HardChallengeSchedule;

namespace Ellen.Launcher.Service.HardChallenge;

[Service(ServiceLifetime.Scoped, typeof(IHardChallengeService))]
public sealed partial class HardChallengeService : IHardChallengeService
{
    private readonly IHardChallengeRepository hardChallengeRepository;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ITaskContext taskContext;
    private readonly ILogger<HardChallengeService> logger;

    private readonly ConcurrentDictionary<PlayerUid, ObservableCollection<HardChallengeView>> hardChallengeCollectionCache = [];
    private readonly AsyncLock collectionLock = new();

    // 国服 game_record 的期次时间按 UTC+8 展示
    private static readonly TimeSpan ServerTimeOffset = TimeSpan.FromHours(8);

    [GeneratedConstructor]
    public partial HardChallengeService(IServiceProvider serviceProvider);

    public async ValueTask<ObservableCollection<HardChallengeView>> GetHardChallengeViewCollectionAsync(HardChallengeMetadataContext context, UserAndUid userAndUid)
    {
        using (await collectionLock.LockAsync().ConfigureAwait(false))
        {
            if (hardChallengeCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<HardChallengeView>? collection))
            {
                return collection;
            }

            await taskContext.SwitchToBackgroundAsync();
            FrozenDictionary<uint, HardChallengeEntry> entryMap = hardChallengeRepository.GetHardChallengeMapByUid(userAndUid.Uid.Value);

            // 随包的 HardChallengeSchedule.json 是静态白名单，新开的一期不在里面。
            // 数据库里的记录自带官方 schedule（名称 + 起止时间），用它补齐元数据，
            // 否则 HardChallengeView 构造时按 Id 索引会直接 KeyNotFoundException。
            foreach (HardChallengeEntry entry in entryMap.Values)
            {
                if (entry.HardChallengeData?.Schedule is { } schedule)
                {
                    EnsureScheduleRegistered(context, schedule);
                }
            }

            ObservableCollection<HardChallengeView> result = OrderedSchedules(context)
                .Select(sch => HardChallengeView.Create(entryMap.GetValueOrDefault(sch.Id), sch, context))
                .ToObservableCollection();

            hardChallengeCollectionCache.TryAdd(userAndUid.Uid, result);
            return result;
        }
    }

    public async ValueTask<ImmutableArray<AvatarView>> GetBlingAvatarsAsync(HardChallengeMetadataContext context, UserAndUid userAndUid)
    {
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            IOverseaSupportFactory<IGameRecordClient> gameRecordClientFactory = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<IGameRecordClient>>();
            IGameRecordClient gameRecordClient = gameRecordClientFactory.Create(userAndUid.IsOversea);

            Response<HardChallengePopularity> response = await gameRecordClient
                .GetHardChallengePopularityAsync(userAndUid)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(response, scope.ServiceProvider, out HardChallengePopularity? webHardChallengePopularity))
            {
                return [];
            }

            return webHardChallengePopularity.AvatarList.SelectAsArray(AvatarView.Create, context);
        }
    }

    public async ValueTask RefreshHardChallengeAsync(HardChallengeMetadataContext context, UserAndUid userAndUid)
    {
        Web.Hoyolab.Takumi.GameRecord.HardChallenge.HardChallenge? webHardChallenge;
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            IOverseaSupportFactory<IGameRecordClient> gameRecordClientFactory = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<IGameRecordClient>>();
            IGameRecordClient gameRecordClient = gameRecordClientFactory.Create(userAndUid.IsOversea);

            // Request the index first
            Response<PlayerInfo> infoResponse = await gameRecordClient
                .GetPlayerInfoAsync(userAndUid)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(infoResponse, scope.ServiceProvider))
            {
                return;
            }

            Response<Web.Hoyolab.Takumi.GameRecord.HardChallenge.HardChallenge> response = await gameRecordClient
                .GetHardChallengeAsync(userAndUid)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(response, scope.ServiceProvider, out webHardChallenge))
            {
                return;
            }
        }

        if (webHardChallenge.Data.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (HardChallengeData hardChallengeData in webHardChallenge.Data)
        {
            // 接口返回的每一期都带官方 schedule，动态登记到本地元数据里，
            // 这样静态白名单过期（新一期上线）时刷新不会再把整条数据丢掉。
            EnsureScheduleRegistered(context, hardChallengeData.Schedule);

            if (!hardChallengeData.SinglePlayer.HasData && !hardChallengeData.MultiPlayer.HasData)
            {
                // 当期还没打过：不写库，但要把这一期放进列表，否则页面永远停在上一期
                await EnsureScheduleViewAsync(context, userAndUid, hardChallengeData.Schedule.ScheduleId).ConfigureAwait(false);
                continue;
            }

            await PrivateRefreshHardChallengeAsync(context, userAndUid, hardChallengeData).ConfigureAwait(false);
        }
    }

    private async ValueTask PrivateRefreshHardChallengeAsync(HardChallengeMetadataContext context, UserAndUid userAndUid, HardChallengeData hardChallengeData)
    {
        if (!hardChallengeCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<HardChallengeView>? hardChallenges))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(context);

        EnsureScheduleRegistered(context, hardChallengeData.Schedule);

        // 新一期的视图原先不存在，旧实现在这里 index < 0 直接 return，
        // 请求成功也既不入库也不上屏；现在先补一个占位视图再更新。
        await EnsureScheduleViewAsync(context, userAndUid, hardChallengeData.Schedule.ScheduleId).ConfigureAwait(false);

        int index = hardChallenges.FirstIndexOf(s => s.ScheduleId == hardChallengeData.Schedule.ScheduleId);
        if (index < 0)
        {
            return;
        }

        HardChallengeView view = hardChallenges[index];

        await taskContext.SwitchToBackgroundAsync();
        HardChallengeEntry targetEntry;
        if (view.Entity is not null)
        {
            view.Entity.HardChallengeData = hardChallengeData;
            hardChallengeRepository.UpdateHardChallengeEntry(view.Entity);
            targetEntry = view.Entity;
        }
        else
        {
            HardChallengeEntry newEntry = HardChallengeEntry.Create(userAndUid.Uid.Value, hardChallengeData);
            hardChallengeRepository.AddHardChallengeEntry(newEntry);
            targetEntry = newEntry;
        }

        await taskContext.SwitchToMainThreadAsync();
        hardChallenges.RemoveAt(index);
        hardChallenges.Insert(index, HardChallengeView.Create(targetEntry, context));
    }

    /// <summary>
    /// 保证列表里存在指定期次的卡片（按开始时间倒序插入）。没有个人记录时用空实体卡片占位，供攻略模式展示。
    /// </summary>
    private async ValueTask EnsureScheduleViewAsync(HardChallengeMetadataContext context, UserAndUid userAndUid, uint scheduleId)
    {
        if (!hardChallengeCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<HardChallengeView>? hardChallenges))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();

        if (hardChallenges.FirstIndexOf(s => s.ScheduleId == scheduleId) >= 0)
        {
            return;
        }

        if (!context.IdHardChallengeScheduleMap.TryGetValue(scheduleId, out MetadataHardChallengeSchedule? meta))
        {
            return;
        }

        List<SchedulePeriod> positions = [.. hardChallenges.Select(static view => new SchedulePeriod(view.ScheduleId, view.ScheduleBegin))];
        int insertAt = ScheduleTimeline.InsertIndex(positions, new SchedulePeriod(scheduleId, meta.Begin));

        hardChallenges.Insert(insertAt, HardChallengeView.Create(null, meta, context));
    }

    /// <summary>
    /// 静态期次表缺当期时，用接口返回的官方 schedule 名称与起止时间登记，不自行编造期次内容。
    /// 表内已有该期时只补空名称，其余字段原样保留。
    /// </summary>
    private void EnsureScheduleRegistered(HardChallengeMetadataContext context, HardChallengeSchedule schedule)
    {
        ImmutableDictionary<HardChallengeScheduleId, MetadataHardChallengeSchedule>? map = context.IdHardChallengeScheduleMap;
        if (map is null || !ScheduleTimeline.IsValidId(schedule.ScheduleId.Value))
        {
            return;
        }

        if (map.TryGetValue(schedule.ScheduleId, out MetadataHardChallengeSchedule? existing))
        {
            string healedName = ScheduleTimeline.ResolveName(existing.Name, schedule.Name);
            if (string.Equals(healedName, existing.Name, StringComparison.Ordinal))
            {
                return;
            }

            context.IdHardChallengeScheduleMap = map.SetItem(schedule.ScheduleId, new MetadataHardChallengeSchedule
            {
                Id = existing.Id,
                Begin = existing.Begin,
                End = existing.End,
                Name = healedName,
            });

            logger.LogInformation("幽境期次 {ScheduleId} 表内名称为空，用接口名称补齐", schedule.ScheduleId.Value);
            return;
        }

        context.IdHardChallengeScheduleMap = map.Add(schedule.ScheduleId, new MetadataHardChallengeSchedule
        {
            Id = schedule.ScheduleId,
            Begin = ToServerTime(schedule.StartTime, schedule.StartDateTime),
            End = ToServerTime(schedule.EndTime, schedule.EndDateTime),
            Name = ScheduleTimeline.ResolveName(null, schedule.Name),
        });

        logger.LogInformation("幽境补登记表外期次 {ScheduleId}（{Name}）", schedule.ScheduleId.Value, schedule.Name);
    }

    private static List<MetadataHardChallengeSchedule> OrderedSchedules(HardChallengeMetadataContext context)
    {
        return
        [
            .. ScheduleTimeline
                .SortDescending(context.IdHardChallengeScheduleMap.Select(static pair => new SchedulePeriod(pair.Key, pair.Value.Begin)))
                .Select(period => context.IdHardChallengeScheduleMap[period.Id])
        ];
    }

    private static DateTimeOffset ToServerTime(long unixSeconds, GameRecordDateTime fallback)
    {
        if (unixSeconds > 0)
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToOffset(ServerTimeOffset);
        }

        // 接口给的是 { year, month, ... } 结构，按国服 UTC+8 组装展示时间
        if (fallback.Year < 1 || fallback.Month is < 1 or > 12 || fallback.Day is < 1 or > 31)
        {
            return default;
        }

        return new DateTimeOffset(fallback.Year, fallback.Month, fallback.Day,
            fallback.Hour, fallback.Minute, fallback.Second, ServerTimeOffset);
    }
}
