// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.ViewModel.GachaLog;

/// <summary>
/// 绝区零调频记录的展示条目
/// </summary>
public sealed class ZZZGachaLogEntry
{
    public required long Id { get; init; }

    public required string Time { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// 稀有度显示文本：S / A / B
    /// </summary>
    public required string RankText { get; init; }

    public required QualityType Quality { get; init; }

    /// <summary>
    /// 所在频段（卡池）名称
    /// </summary>
    public required string ChannelName { get; init; }
}
