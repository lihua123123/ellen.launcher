// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

/// <summary>
/// 圣遗物副词条统计卡片：包含该角色全部圣遗物副词条按词条名汇总后的数据。
/// 作为圣遗物网格中的最后一张卡片显示，尺寸与普通圣遗物卡片一致。
/// </summary>
public sealed class ReliquarySubPropertySummaryCard
{
    public ReliquarySubPropertySummaryCard(ImmutableArray<ReliquarySubPropertySummary> summaries)
    {
        Summaries = summaries;
    }

    public ImmutableArray<ReliquarySubPropertySummary> Summaries { get; }
}
