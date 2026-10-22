// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.ViewModel.GachaLog;

/// <summary>
/// 绝区零单个频段的统计摘要
/// </summary>
public sealed class ZZZWishSummary
{
    public required string ChannelName { get; init; }

    public required int TotalCount { get; init; }

    /// <summary>S 级（金）抽数</summary>
    public required int SCount { get; init; }

    /// <summary>A 级（紫）抽数</summary>
    public required int ACount { get; init; }

    /// <summary>B 级（蓝）抽数</summary>
    public required int BCount { get; init; }

    /// <summary>距上一个 S 级的抽数（保底进度）</summary>
    public required int PitySinceLastS { get; init; }

    /// <summary>上一个 S 级物品名称</summary>
    public required string LastSItemName { get; init; }

    /// <summary>是否为空频段（无记录）</summary>
    public bool IsEmpty => TotalCount == 0;
}
