// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Globalization;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

/// <summary>
/// 圣遗物副词条统计的一行：某类词条（已合并大/小词条，如「生命值」同时包含百分比与固定值）
/// 在该角色全部圣遗物副词条上的词条值（强化数）之和。
/// </summary>
public sealed class ReliquarySubPropertySummary
{
    public ReliquarySubPropertySummary(string name, double count)
    {
        Name = name;
        Count = count;
    }

    public string Name { get; }

    /// <summary>
    /// 该类词条的词条值之和（强化数，单位：1 次满档强化 = 1）。
    /// </summary>
    public double Count { get; }

    public string CountText => Count.ToString("0.0", CultureInfo.CurrentCulture);
}
