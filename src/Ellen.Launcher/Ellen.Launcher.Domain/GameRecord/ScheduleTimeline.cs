// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ellen.Launcher.Domain.GameRecord;

/// <summary>
/// 期次时间线上的一个位置：期次 Id 与开始时间。
/// </summary>
/// <remarks>
/// 三张随包排期表（深境 / 剧诗 / 幽境）都是「Id 递增 + 开始时间递增」的静态白名单，
/// 新一期上线后表里没有这一期。这里只做纯数据决策，不引用任何 UI 类型，
/// 因此可以被离线 fixture 直接驱动（见 Ellen.Launcher.Test）。
/// </remarks>
public readonly record struct SchedulePeriod(uint Id, DateTimeOffset Begin);

/// <summary>
/// 列表里一项的选中依据。
/// </summary>
public readonly record struct ScheduleSelection(uint Id, bool Engaged);

/// <summary>
/// 期次登记 / 排序 / 命名 / 默认选中的纯策略。
/// </summary>
public static class ScheduleTimeline
{
    /// <summary>
    /// 期次倒序比较器：开始时间晚的在前，同一时间按 Id 倒序。
    /// </summary>
    /// <remarks>
    /// 三张表的历史数据满足「Id 越大开始时间越晚」，所以这个排序与旧的
    /// <c>OrderByDescending(ScheduleId)</c> 对已有期完全相同（fixture 逐项比对过），
    /// 但对表外新期也能按官方开始时间插到正确位置。
    /// </remarks>
    public static readonly IComparer<SchedulePeriod> Descending = Comparer<SchedulePeriod>.Create(static (left, right) =>
    {
        int byBegin = right.Begin.CompareTo(left.Begin);
        return byBegin != 0 ? byBegin : right.Id.CompareTo(left.Id);
    });

    /// <summary>
    /// 期次 Id 是否有效。
    /// </summary>
    /// <remarks>
    /// 接口在没有当期数据时可能回 <c>schedule_id = 0</c>，登记进去就是一张「第 0 期」的空卡片，
    /// 所以 0 一律不当作期次处理。
    /// </remarks>
    public static bool IsValidId(uint id)
    {
        return id != 0;
    }

    /// <summary>
    /// 该期次是否需要补登记：本地表里没有才登记。
    /// </summary>
    /// <remarks>
    /// 已有期一律不覆盖——静态表字段更全（深境有层 / 间数据，剧诗有元素与特邀），
    /// 用接口的残缺数据盖掉只会让已有期变差。因此重复调用是幂等的。
    /// </remarks>
    public static bool NeedsRegistration(IEnumerable<uint> existingIds, uint id)
    {
        return !existingIds.Contains(id);
    }

    /// <summary>
    /// 新期次在倒序列表里的插入位置。
    /// </summary>
    public static int InsertIndex(IReadOnlyList<SchedulePeriod> descendingPeriods, SchedulePeriod incoming)
    {
        for (int index = 0; index < descendingPeriods.Count; index++)
        {
            if (Descending.Compare(incoming, descendingPeriods[index]) < 0)
            {
                return index;
            }
        }

        return descendingPeriods.Count;
    }

    /// <summary>
    /// 按 <see cref="Descending"/> 排成倒序列表（稳定排序，同键保持入参顺序）。
    /// </summary>
    public static List<SchedulePeriod> SortDescending(IEnumerable<SchedulePeriod> periods)
    {
        return [.. periods.OrderBy(static period => period, Descending)];
    }

    /// <summary>
    /// 计算生效名称：表内名称优先，表内为空才用接口名称；两者都为空返回空串。
    /// </summary>
    /// <remarks>
    /// 返回空串而不是编一个名字：视图层再套兜底文案（「第 N 期：未命名」），
    /// 这样「没有名字」在任何层都不会被伪装成一个看似真实的期次名。
    /// </remarks>
    public static string ResolveName(string? tableName, string? apiName)
    {
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            return tableName;
        }

        return string.IsNullOrWhiteSpace(apiName) ? string.Empty : apiName;
    }

    /// <summary>
    /// 起止时间展示；任一端缺失时返回 <paramref name="unknownText"/>，不打印 0001.01.01 这种假时间。
    /// </summary>
    public static string FormatTimeRange(DateTimeOffset begin, DateTimeOffset end, string unknownText)
    {
        if (begin == default || end == default)
        {
            return unknownText;
        }

        return $"{begin:yyyy.MM.dd HH:mm} - {end:yyyy.MM.dd HH:mm}";
    }

    /// <summary>
    /// 默认选中项的下标（列表为倒序，0 即最新一期）。
    /// </summary>
    /// <param name="descendingItems">按 <see cref="Descending"/> 排好的列表项。</param>
    /// <param name="currentId">调用前用户正在看的期次；为 <see langword="null"/> 表示没有既有选择。</param>
    /// <returns>下标；空列表返回 <c>-1</c>。</returns>
    /// <remarks>
    /// 顺序：用户已选且仍在列表里 → 保持不动；否则最近一期有记录的；否则最新一期。
    /// 「保持不动」优先，避免刷新一次就把用户翻到的历史期顶掉。
    /// <para>
    /// 注意这是**账号级**口径：最新一期没有记录时**不强制**选中它，而是退到最近一期有记录的。
    /// 深境 / 剧诗没有攻略兜底，新期一开、当期还没打，强制选最新会让首页变空白。
    /// 该取舍由测试 <c>NewestPeriodWithoutRecordIsNotForced</c> 锁定，改之前先确认三页的空态。
    /// </para>
    /// </remarks>
    public static int SelectDefaultIndex(IReadOnlyList<ScheduleSelection> descendingItems, uint? currentId)
    {
        if (descendingItems.Count == 0)
        {
            return -1;
        }

        if (currentId is { } id)
        {
            for (int index = 0; index < descendingItems.Count; index++)
            {
                if (descendingItems[index].Id == id)
                {
                    return index;
                }
            }
        }

        for (int index = 0; index < descendingItems.Count; index++)
        {
            if (descendingItems[index].Engaged)
            {
                return index;
            }
        }

        return 0;
    }
}
