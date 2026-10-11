// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;
using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

/// <summary>
/// 把某角色全部圣遗物的副词条，按词条名（大/小词条已合并，如「生命值」）汇总其词条值（强化数）之和，
/// 并按词条值从大到小排序。仅统计副词条，不含主属性。
/// </summary>
public static class ReliquarySubPropertySummaryFactory
{
    public static ReliquarySubPropertySummaryCard? Compute(ImmutableArray<ReliquaryView> reliquaries)
    {
        if (reliquaries.IsEmpty)
        {
            return null;
        }

        Dictionary<string, double> sums = new();

        foreach (ReliquaryView reliquary in reliquaries)
        {
            foreach (ReliquaryComposedSubProperty subProperty in reliquary.ComposedSubProperties)
            {
                if (subProperty.Name is { Length: > 0 } name)
                {
                    sums.TryGetValue(name, out double existing);
                    sums[name] = existing + subProperty.EnhancementCount;
                }
            }
        }

        ImmutableArray<ReliquarySubPropertySummary> summaries = sums
            .Select(pair => new ReliquarySubPropertySummary(pair.Key, pair.Value))
            .OrderByDescending(summary => summary.Count)
            .ToImmutableArray();

        return summaries.IsEmpty ? null : new ReliquarySubPropertySummaryCard(summaries);
    }
}
