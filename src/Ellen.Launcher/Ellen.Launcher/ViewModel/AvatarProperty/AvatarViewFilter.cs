// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Intrinsic.Frozen;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;
using System.Collections.ObjectModel;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public static class AvatarViewFilter
{
    public static Predicate<AvatarView>? Compile(SearchData? searchData)
    {
        return searchData is { FilterTokens.Count: > 0 } ? Compile(searchData.FilterTokens) : default;
    }

    public static Predicate<AvatarView> Compile(ObservableCollection<SearchToken> input)
    {
        return avatar => DoFilter(input, avatar);
    }

    private static bool DoFilter(ObservableCollection<SearchToken> input, AvatarView avatarView)
    {
        List<bool> matches = [];

        foreach ((SearchTokenKind kind, IEnumerable<string> tokens) in input.GroupBy(token => token.Kind, token => token.Value))
        {
            switch (kind)
            {
                case SearchTokenKind.ElementName:
                    if (IntrinsicFrozen.ElementNames.Overlaps(tokens))
                    {
                        matches.Add(tokens.Contains(avatarView.Element.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)));
                    }

                    break;
                case SearchTokenKind.WeaponType:
                    if (IntrinsicFrozen.WeaponTypes.Overlaps(tokens))
                    {
                        ArgumentNullException.ThrowIfNull(avatarView.Weapon);
                        matches.Add(tokens.Contains(avatarView.Weapon.WeaponType.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)));
                    }

                    break;
                case SearchTokenKind.ItemQuality:
                    if (IntrinsicFrozen.ItemQualities.Overlaps(tokens))
                    {
                        string? qualityName = avatarView.Quality.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture);
                        bool qualityMatch = qualityName is not null && tokens.Contains(qualityName);

                        // ZZZ 的 S 级仍保持 5 星品质，限定 S 额外命中“限定五星” token；
                        // 这样“五星”既能包含限定也能包含常驻。
                        if (!qualityMatch && avatarView.IsZZZLimited)
                        {
                            string? limitedName = QualityType.QUALITY_ORANGE_SP.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture);
                            qualityMatch = limitedName is not null && tokens.Contains(limitedName);
                        }

                        matches.Add(qualityMatch);
                    }

                    break;
                case SearchTokenKind.ZZZProfession:
                    matches.Add(tokens.Contains(avatarView.ZZZProfession.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)));
                    break;
                case SearchTokenKind.ZZZElement:
                    matches.Add(tokens.Contains(avatarView.ZZZElement.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)));
                    break;
                case SearchTokenKind.None:
                    matches.Add(tokens.Any(token => avatarView.Name.Contains(token, StringComparison.OrdinalIgnoreCase)));
                    break;
                default:
                    matches.Add(false);
                    break;
            }
        }

        return matches.Count > 0 && matches.Aggregate((a, b) => a && b);
    }
}