// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.GachaLog;

public sealed partial class HistoryWish : Wish, IPropertyValuesProvider
{
    public required string Version { get; init; }

    public required Uri BannerImage { get; init; }

    /// <summary>
    /// 绝区零没有卡池横幅图，历史详情页隐藏横幅图片区域。
    /// </summary>
    public bool HasBannerImage { get; init; } = true;

    public required ImmutableArray<StatisticsItem> OrangeUpList { get; init; }

    public required ImmutableArray<StatisticsItem> PurpleUpList { get; init; }

    public required ImmutableArray<StatisticsItem> SummaryList { get; init; }

    public required ImmutableArray<StatisticsItem> PurpleList { get; init; }

    public required ImmutableArray<StatisticsItem> BlueList { get; init; }
}