// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Monster;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;

namespace Ellen.Launcher.ViewModel.Wiki;

// ReSharper disable PossibleMultipleEnumeration
public static class MonsterFilter
{
    public static Predicate<Monster>? Compile(SearchData? searchData)
    {
        return searchData is { FilterTokens.Count: > 0 } ? CompileTokens(searchData.FilterTokens) : default;
    }

    private static Predicate<Monster> CompileTokens(IEnumerable<SearchToken> input)
    {
        ILookup<SearchTokenKind, string> lookup = input.ToLookup(token => token.Kind, token => token.Value);
        return monster => CompileTokenLookup(lookup, monster);
    }

    private static bool CompileTokenLookup(ILookup<SearchTokenKind, string> lookup, Monster monster)
    {
        List<bool> matches = [];

        foreach ((SearchTokenKind kind, IEnumerable<string> tokens) in lookup)
        {
            switch (kind)
            {
                case SearchTokenKind.Monster:
                    matches.Add(tokens.Contains(monster.Name));
                    break;
                case SearchTokenKind.None:
                    matches.Add(tokens.Any(token => monster.Name?.Contains(token, StringComparison.OrdinalIgnoreCase) == true));
                    break;
                default:
                    matches.Add(false);
                    break;
            }
        }

        return matches.Count > 0 && matches.All(r => r);
    }

}
