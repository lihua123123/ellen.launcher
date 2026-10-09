// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.ViewModel.GachaLog;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

namespace Ellen.Launcher.Service.GachaLog.Factory;

public sealed class HistoryWishBuilder
{
    private readonly GachaEvent gachaEvent;

    private readonly Dictionary<IStatisticsItemConvertible, int> orangeUpCounter = [];
    private readonly Dictionary<IStatisticsItemConvertible, int> purpleUpCounter = [];

    // 按抽到顺序逐条记录每次获取（含「本期第几抽」），而非按物品聚合计数
    private readonly List<StatisticsItem> orangeObtained = [];
    private readonly List<StatisticsItem> purpleObtained = [];
    private readonly List<StatisticsItem> blueObtained = [];

    private int totalCountTracker;

    // 上一次抽到该品质物品时的抽数（1 起），用来算「这次抽到它花了多少抽」
    private int lastOrangePullIndex;
    private int lastPurplePullIndex;
    private int lastBluePullIndex;

    private HistoryWishBuilder(GachaEvent gachaEvent, GachaLogServiceMetadataContext context)
    {
        this.gachaEvent = gachaEvent;
        ConfigType = gachaEvent.Type;

        switch (ConfigType)
        {
            case GachaType.ActivityAvatar or GachaType.SpecialActivityAvatar:
                orangeUpCounter = gachaEvent.UpOrangeList.Select(id => context.IdAvatarMap[id]).ToDictionary(IStatisticsItemConvertible (a) => a, _ => 0);
                purpleUpCounter = gachaEvent.UpPurpleList.Select(id => context.IdAvatarMap[id]).ToDictionary(IStatisticsItemConvertible (a) => a, _ => 0);
                break;
            case GachaType.ActivityWeapon:
                orangeUpCounter = gachaEvent.UpOrangeList.Select(id => context.IdWeaponMap[id]).ToDictionary(IStatisticsItemConvertible (w) => w, _ => 0);
                purpleUpCounter = gachaEvent.UpPurpleList.Select(id => context.IdWeaponMap[id]).ToDictionary(IStatisticsItemConvertible (w) => w, _ => 0);
                break;
            case GachaType.ActivityCity:

                // Avatars are less than weapons, so we try to get the value from avatar map first
                orangeUpCounter = gachaEvent.UpOrangeList.Select(id => (IStatisticsItemConvertible?)context.IdAvatarMap.GetValueOrDefault(id) ?? context.IdWeaponMap[id]).ToDictionary(c => c, _ => 0);
                purpleUpCounter = gachaEvent.UpPurpleList.Select(id => (IStatisticsItemConvertible?)context.IdAvatarMap.GetValueOrDefault(id) ?? context.IdWeaponMap[id]).ToDictionary(c => c, _ => 0);
                break;
        }
    }

    public GachaType ConfigType { get; }

    public DateTimeOffset From { get => gachaEvent.From; }

    public DateTimeOffset To { get => gachaEvent.To; }

    public bool IsEmpty { get => totalCountTracker <= 0; }

    public static HistoryWishBuilder Create(GachaEvent gachaEvent, GachaLogServiceMetadataContext context)
    {
        return new(gachaEvent, context);
    }

    public bool IncreaseOrange(IStatisticsItemConvertible item)
    {
        orangeObtained.Add(CreateObtainedItem(item, ref lastOrangePullIndex));
        ++totalCountTracker;

        return orangeUpCounter.TryIncreaseByOne(item);
    }

    public void IncreasePurple(IStatisticsItemConvertible item)
    {
        purpleUpCounter.TryIncreaseByOne(item);
        purpleObtained.Add(CreateObtainedItem(item, ref lastPurplePullIndex));
        ++totalCountTracker;
    }

    public void IncreaseBlue(IStatisticsItemConvertible item)
    {
        blueObtained.Add(CreateObtainedItem(item, ref lastBluePullIndex));
        ++totalCountTracker;
    }

    /// <summary>
    /// 记录一次获取：<see cref="StatisticsItem.PullCount"/> 是「这次抽到它花了多少抽」，
    /// 也就是本次抽数减去上一次抽到**同品质**物品的抽数（本期第一次出该品质就从开头算起）。
    /// </summary>
    private StatisticsItem CreateObtainedItem(IStatisticsItemConvertible item, ref int lastPullIndexOfSameRarity)
    {
        int pullIndex = totalCountTracker + 1;
        StatisticsItem statisticsItem = item.ToStatisticsItem(1);
        statisticsItem.PullCount = pullIndex - lastPullIndexOfSameRarity;
        lastPullIndexOfSameRarity = pullIndex;
        return statisticsItem;
    }

    public HistoryWish ToHistoryWish()
    {
        HistoryWish historyWish = new()
        {
            // Base
            Name = gachaEvent.Name,
            From = gachaEvent.From,
            To = gachaEvent.To,
            TotalCount = totalCountTracker,

            // Fill
            Version = gachaEvent.Version,
            BannerImage = gachaEvent.Banner,
            OrangeUpList = orangeUpCounter.ToStatisticsImmutableArray(),
            PurpleUpList = purpleUpCounter.ToStatisticsImmutableArray(),
            SummaryList = [.. orangeObtained],
            PurpleList = [.. purpleObtained],
            BlueList = [.. blueObtained],
        };

        return historyWish;
    }
}