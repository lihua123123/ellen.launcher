// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.ViewModel.Cultivation;

namespace Ellen.Launcher.Service.Cultivation;

[Service(ServiceLifetime.Singleton, typeof(ICultivationResinStatisticsService))]
public sealed partial class CultivationResinStatisticsService : ICultivationResinStatisticsService
{
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial CultivationResinStatisticsService(IServiceProvider serviceProvider);

    public async ValueTask<ResinStatistics> GetResinStatisticsAsync(IEnumerable<StatisticsCultivateItem> statisticsCultivateItems, CancellationToken token)
    {
        await taskContext.SwitchToBackgroundAsync();
        ResinStatistics statistics = new();

        ILookup<uint, StatisticsCultivateItem> itemsLookup = statisticsCultivateItems
            .Where(i => i.Inner.IsResinItem())
            .ToLookup(i => i.Inner.Rank);

        foreach ((uint rank, IEnumerable<StatisticsCultivateItem> items) in itemsLookup)
        {
            switch (rank)
            {
                case 10U: // 摩拉
                    {
                        StatisticsCultivateItem item = items.Single();
                        if (!item.IsFinished)
                        {
                            double times = GetStatisticsCultivateItemTimes(item);
                            statistics.BlossomOfWealth.RawItemCount += times;
                        }

                        break;
                    }

                case 100U: // 经验
                    {
                        // TODO: Reduce enumeration
                        if (items.SingleOrDefault(i => i.Inner.RankLevel is QualityType.QUALITY_PURPLE) is { IsFinished: false })
                        {
                            foreach (StatisticsCultivateItem item in items)
                            {
                                double experience = item.Inner.RankLevel switch
                                {
                                    QualityType.QUALITY_GREEN => 1000D,
                                    QualityType.QUALITY_BLUE => 5000D,
                                    QualityType.QUALITY_PURPLE => 20000D,
                                    _ => throw EllenException.NotSupported(),
                                };
                                double times = GetStatisticsCultivateItemTimes(item) * experience;
                                statistics.BlossomOfRevelation.RawItemCount += times;
                            }
                        }

                        break;
                    }

                case 11101U: // BOSS 掉落
                    {
                        foreach (StatisticsCultivateItem item in items)
                        {
                            if (!item.IsFinished)
                            {
                                double times = GetStatisticsCultivateItemTimes(item);
                                // 元数据里 Boss / 周本材料 RankLevel 可能缺失（恒为 QUALITY_WHITE），
                                // 未知品质跳过，避免统计刷新抛异常。
                                switch (item.Inner.RankLevel)
                                {
                                    case QualityType.QUALITY_PURPLE:
                                        statistics.NormalBoss.RawItemCount += times;
                                        break;
                                    case QualityType.QUALITY_ORANGE:
                                        statistics.WeeklyBoss.RawItemCount += times;
                                        break;
                                }
                            }
                        }

                        break;
                    }

                default:
                    {
                        // 天赋书，武器突破材料
                        // items.Count in [0..4]
                        double greenItems = 0D;
                        double blueItems = 0D;
                        double purpleItems = 0D;
                        double orangeItems = 0D;

                        // ABCDE -> B；未知分类（如智识之冕等 rank 非天赋书/武器突破的材料）跳过，
                        // 避免自定义材料计划带来的新种类触发 NotSupported 崩溃。
                        ResinStatisticsItem? targetStatisticsItem = ((rank / 1000) % 10) switch
                        {
                            3 => statistics.TalentAscension,
                            5 => statistics.WeaponAscension,
                            _ => null,
                        };

                        if (targetStatisticsItem is null)
                        {
                            break;
                        }

                        foreach (StatisticsCultivateItem item in items)
                        {
                            // IsFinished is implicitly calculated in AlchemyCrafting
                            double times = GetStatisticsCultivateItemTimes(item);
                            // 元数据里部分材料（尤其自定义材料计划涉及的树脂素材）RankLevel 缺失
                            // （恒为 QUALITY_WHITE），未知品质直接跳过，避免统计刷新时抛异常导致崩溃。
                            switch (item.Inner.RankLevel)
                            {
                                case QualityType.QUALITY_GREEN:
                                    greenItems += times;
                                    break;
                                case QualityType.QUALITY_BLUE:
                                    blueItems += times;
                                    break;
                                case QualityType.QUALITY_PURPLE:
                                    purpleItems += times;
                                    break;
                                case QualityType.QUALITY_ORANGE:
                                    orangeItems += times;
                                    break;
                            }
                        }

                        targetStatisticsItem!.RawItemCount += AlchemyCrafting.UnWeighted(orangeItems, purpleItems, blueItems, greenItems);
                        break;
                    }
            }
        }

        statistics.RefreshBlossomOfWealth();
        return statistics;
    }

    private static double GetStatisticsCultivateItemTimes(StatisticsCultivateItem item)
    {
        return item.Count - (long)item.Current;
    }
}