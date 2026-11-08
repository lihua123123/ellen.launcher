// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.EnemyArchive;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;

namespace Ellen.Launcher.ViewModel.Wiki;

// ReSharper disable PossibleMultipleEnumeration
public static class MonsterFilter
{
    public static Predicate<MonsterCatalogEntry>? Compile(SearchData? searchData)
    {
        return searchData is { FilterTokens.Count: > 0 } ? CompileTokens(searchData.FilterTokens) : default;
    }

    /// <summary>
    /// P2：753 条网页版筛选。token 命中展示项的 SearchTokens
    /// （name/nameEn/aliases/tags/categoryLabel/旧中文分类/旧 InternalName）。
    /// 旧分类中文名保留作搜索兼容，分组仍以网页 categoryLabel 为准。
    /// </summary>
    public static Predicate<Domain.WikiSource.WikiEnemyViewItem>? CompileForWeb(SearchData? searchData)
    {
        if (searchData is not { FilterTokens.Count: > 0 })
        {
            return default;
        }

        string[] tokens = [.. searchData.FilterTokens.Select(static t => t.Value).Where(static t => !string.IsNullOrWhiteSpace(t))];
        return item => tokens.All(token => item.SearchTokens.Any(candidate => candidate.Contains(token, StringComparison.OrdinalIgnoreCase)));
    }

    private static Predicate<MonsterCatalogEntry> CompileTokens(IEnumerable<SearchToken> input)
    {
        ILookup<SearchTokenKind, string> lookup = input.ToLookup(token => token.Kind, token => token.Value);
        return entry => CompileTokenLookup(lookup, entry);
    }

    private static bool CompileTokenLookup(ILookup<SearchTokenKind, string> lookup, MonsterCatalogEntry entry)
    {
        List<bool> matches = [];

        foreach ((SearchTokenKind kind, IEnumerable<string> tokens) in lookup)
        {
            switch (kind)
            {
                case SearchTokenKind.Monster:
                    matches.Add(tokens.Contains(entry.Name));
                    break;
                case SearchTokenKind.None:
                    matches.Add(tokens.Any(token => entry.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));
                    break;
                default:
                    matches.Add(false);
                    break;
            }
        }

        return matches.Count > 0 && matches.All(r => r);
    }
}
