// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Domain.GameRecord;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.ViewModel.RoleCombat;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat;
using Ellen.Launcher.Web.Response;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using MetadataRoleCombatSchedule = Ellen.Launcher.Model.Metadata.RoleCombatSchedule;

namespace Ellen.Launcher.Service.RoleCombat;

[Service(ServiceLifetime.Scoped, typeof(IRoleCombatService))]
public sealed partial class RoleCombatService : IRoleCombatService
{
    private readonly IRoleCombatRepository roleCombatRepository;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ITaskContext taskContext;
    private readonly ILogger<RoleCombatService> logger;

    private readonly ConcurrentDictionary<PlayerUid, ObservableCollection<RoleCombatView>> roleCombatCollectionCache = [];
    private readonly AsyncLock collectionLock = new();

    // 国服 game_record 的期次时间按 UTC+8 展示
    private static readonly TimeSpan ServerTimeOffset = TimeSpan.FromHours(8);

    [GeneratedConstructor]
    public partial RoleCombatService(IServiceProvider serviceProvider);

    public async ValueTask<ObservableCollection<RoleCombatView>> GetRoleCombatViewCollectionAsync(RoleCombatMetadataContext context, UserAndUid userAndUid)
    {
        using (await collectionLock.LockAsync().ConfigureAwait(false))
        {
            if (roleCombatCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<RoleCombatView>? collection))
            {
                return collection;
            }

            await taskContext.SwitchToBackgroundAsync();
            FrozenDictionary<uint, RoleCombatEntry> entryMap = roleCombatRepository.GetRoleCombatEntryMapByUid(userAndUid.Uid.Value);

            // 静态表之外的期次（库里有记录、表里没有）先用记录自带的官方起止时间补登记，
            // 否则 RoleCombatView 构造时按 Id 索引会 KeyNotFoundException，整条记录上不了屏。
            foreach (RoleCombatEntry entry in entryMap.Values)
            {
                if (entry.RoleCombatData?.Schedule is { } recorded)
                {
                    EnsureScheduleRegistered(context, recorded.ScheduleId, recorded.StartTime, recorded.EndTime);
                }
            }

            ObservableCollection<RoleCombatView> result = OrderedSchedules(context)
                .Select(schedule => RoleCombatView.Create(entryMap.GetValueOrDefault(schedule.Id), schedule, context))
                .ToObservableCollection();

            roleCombatCollectionCache.TryAdd(userAndUid.Uid, result);
            return result;
        }
    }

    public async ValueTask RefreshRoleCombatAsync(RoleCombatMetadataContext context, UserAndUid userAndUid)
    {
        Web.Hoyolab.Takumi.GameRecord.RoleCombat.RoleCombat? webRoleCombat;
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

            Response<Web.Hoyolab.Takumi.GameRecord.RoleCombat.RoleCombat> response = await gameRecordClient
                .GetRoleCombatAsync(userAndUid)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(response, scope.ServiceProvider, out webRoleCombat))
            {
                return;
            }
        }

        if (webRoleCombat.Data.IsDefaultOrEmpty)
        {
            return;
        }

        foreach (RoleCombatData roleCombatData in webRoleCombat.Data)
        {
            // 接口返回的每一期都带官方 schedule，先动态登记，静态表过期时也不会丢数据
            EnsureScheduleRegistered(context, roleCombatData.Schedule.ScheduleId, roleCombatData.Schedule.StartTime, roleCombatData.Schedule.EndTime);

            if (!roleCombatData.HasData)
            {
                // 当期还没打：不写库，但要把这一期放进列表，否则页面永远停在上一期
                await EnsureScheduleViewAsync(context, userAndUid, roleCombatData.Schedule.ScheduleId).ConfigureAwait(false);
                continue;
            }

            await PrivateRefreshRoleCombatAsync(context, userAndUid, roleCombatData).ConfigureAwait(false);
        }
    }

    private async ValueTask PrivateRefreshRoleCombatAsync(RoleCombatMetadataContext context, UserAndUid userAndUid, RoleCombatData roleCombatData)
    {
        if (!roleCombatCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<RoleCombatView>? roleCombats))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(context);

        EnsureScheduleRegistered(context, roleCombatData.Schedule.ScheduleId, roleCombatData.Schedule.StartTime, roleCombatData.Schedule.EndTime);
        await EnsureScheduleViewAsync(context, userAndUid, roleCombatData.Schedule.ScheduleId).ConfigureAwait(false);

        int index = roleCombats.FirstIndexOf(s => s.ScheduleId == roleCombatData.Schedule.ScheduleId);
        if (index < 0)
        {
            return;
        }

        await taskContext.SwitchToBackgroundAsync();
        RoleCombatView view = roleCombats[index];

        RoleCombatEntry targetEntry;
        if (view.Entity is not null)
        {
            view.Entity.RoleCombatData = roleCombatData;
            roleCombatRepository.UpdateRoleCombatEntry(view.Entity);
            targetEntry = view.Entity;
        }
        else
        {
            RoleCombatEntry newEntry = RoleCombatEntry.Create(userAndUid.Uid.Value, roleCombatData);
            roleCombatRepository.AddRoleCombatEntry(newEntry);
            targetEntry = newEntry;
        }

        await taskContext.SwitchToMainThreadAsync();
        roleCombats.RemoveAt(index);
        roleCombats.Insert(index, RoleCombatView.Create(targetEntry, context));
    }

    /// <summary>
    /// 保证列表里存在指定期次的卡片（按开始时间倒序插入）。没有个人记录时用空实体卡片占位。
    /// </summary>
    private async ValueTask EnsureScheduleViewAsync(RoleCombatMetadataContext context, UserAndUid userAndUid, uint scheduleId)
    {
        if (!roleCombatCollectionCache.TryGetValue(userAndUid.Uid, out ObservableCollection<RoleCombatView>? roleCombats))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();

        if (roleCombats.FirstIndexOf(s => s.ScheduleId == scheduleId) >= 0)
        {
            return;
        }

        if (!context.IdRoleCombatScheduleMap.TryGetValue(scheduleId, out MetadataRoleCombatSchedule? meta))
        {
            return;
        }

        List<SchedulePeriod> positions = [.. roleCombats.Select(static view => new SchedulePeriod(view.ScheduleId, view.ScheduleBegin))];
        int insertAt = ScheduleTimeline.InsertIndex(positions, new SchedulePeriod(scheduleId, meta.Begin));

        roleCombats.Insert(insertAt, RoleCombatView.Create(null, meta, context));
    }

    /// <summary>
    /// 静态期次表缺该期时，用接口返回的官方起止时间登记，不自行编造期次内容。
    /// </summary>
    private void EnsureScheduleRegistered(RoleCombatMetadataContext context, uint scheduleId, long startTime, long endTime)
    {
        ImmutableDictionary<RoleCombatScheduleId, MetadataRoleCombatSchedule>? map = context.IdRoleCombatScheduleMap;
        if (map is null || !ScheduleTimeline.IsValidId(scheduleId) || !ScheduleTimeline.NeedsRegistration(map.Keys.Select(static key => (uint)key), scheduleId))
        {
            // 0 不是有效期次：接口在没有数据时可能回 0，登记进列表会变成一张「第 0 期」的空卡片
            return;
        }

        // 剧诗的元素与特邀 / 开幕名单只有静态表有（接口不给），表外新期留空，
        // 不拿别的期的名单顶替。
        context.IdRoleCombatScheduleMap = map.Add(scheduleId, new MetadataRoleCombatSchedule
        {
            Id = scheduleId,
            Begin = ToServerTime(startTime),
            End = ToServerTime(endTime),
            Elements = [],
            SpecialAvatars = [],
            InitialAvatars = [],
        });

        logger.LogInformation("剧诗补登记表外期次 {ScheduleId}（{Begin:o} - {End:o}）", scheduleId, ToServerTime(startTime), ToServerTime(endTime));
    }

    private static List<MetadataRoleCombatSchedule> OrderedSchedules(RoleCombatMetadataContext context)
    {
        return
        [
            .. ScheduleTimeline
                .SortDescending(context.IdRoleCombatScheduleMap.Select(static pair => new SchedulePeriod(pair.Key, pair.Value.Begin)))
                .Select(period => context.IdRoleCombatScheduleMap[period.Id])
        ];
    }

    private static DateTimeOffset ToServerTime(long unixSeconds)
    {
        // 缺失（0 / 负数）时保留 default，由视图显示「时间未知」，不编一个看起来正常的时间
        return unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToOffset(ServerTimeOffset) : default;
    }
}
