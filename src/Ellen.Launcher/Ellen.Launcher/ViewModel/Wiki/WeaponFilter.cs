// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic.Frozen;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.Wiki;

// ReSharper disable PossibleMultipleEnumeration
public static class WeaponFilter
{
    public static Predicate<Weapon>? Compile(SearchData? searchData)
    {
        return searchData is { FilterTokens.Count: > 0 } ? Compile(searchData.FilterTokens) : default;
    }

    public static Predicate<Weapon> Compile(IEnumerable<SearchToken> input)
    {
        ILookup<SearchTokenKind, string> lookup = input.ToLookup(token => token.Kind, token => token.Value);
        return weapon => DoFilter(lookup, weapon);
    }

    private static bool DoFilter(ILookup<SearchTokenKind, string> lookup, Weapon weapon)
    {
        List<bool> matches = [];

        // Tokens is a BCL public Grouping<string>, enumerating will use an public PartialArrayEnumerator<string>
        foreach ((SearchTokenKind kind, IEnumerable<string> tokens) in lookup)
        {
            switch (kind)
            {
                case SearchTokenKind.WeaponType:
                    if (IntrinsicFrozen.WeaponTypes.Overlaps(tokens))
                    {
                        matches.Add(tokens.Contains(weapon.WeaponType.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)));
                    }

                    break;
                case SearchTokenKind.ItemQuality:
                    if (IntrinsicFrozen.ItemQualities.Overlaps(tokens))
                    {
                        matches.Add(tokens.Contains(weapon.Quality.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)));
                    }

                    break;
                case SearchTokenKind.FightProperty:
                    if (IntrinsicFrozen.FightProperties.Overlaps(tokens))
                    {
                        matches.Add(tokens.Contains(weapon.GrowCurves.Array.ElementAtOrDefault(1)?.Type.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)));
                    }

                    break;
                case SearchTokenKind.Weapon:
                    matches.Add(tokens.Contains(weapon.Name));
                    break;
                case SearchTokenKind.None:
                    matches.Add(tokens.Any(token => weapon.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));
                    break;
                default:
                    matches.Add(false);
                    break;
            }
        }

        return matches.Count > 0 && matches.All(r => r);
    }
}