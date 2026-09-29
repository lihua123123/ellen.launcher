// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.UI.Xaml.Control.AutoSortBox;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Backpack;

public static class BackpackSortTokenBuilder
{
    public static ImmutableDictionary<BackpackItemCategory, ImmutableArray<AutoSortToken>> Build()
    {
        ImmutableDictionary<BackpackItemCategory, ImmutableArray<AutoSortToken>>.Builder builder =
            ImmutableDictionary.CreateBuilder<BackpackItemCategory, ImmutableArray<AutoSortToken>>();

        builder.Add(BackpackItemCategory.Weapon,
        [
            new(AutoSortTokenKind.Quality, SH.ViewPageBackpackSortQuality),
            new(AutoSortTokenKind.Level, SH.ViewPageBackpackSortLevel),
            new(AutoSortTokenKind.Refinement, SH.ViewPageBackpackSortRefinement),
            new(AutoSortTokenKind.WeaponType, SH.ViewPageBackpackSortWeaponType),
            new(AutoSortTokenKind.Name, SH.ViewPageBackpackSortName),
            new(AutoSortTokenKind.Lock, SH.ViewPageBackpackSortLock),
            new(AutoSortTokenKind.Count, SH.ViewPageBackpackSortCount),
        ]);

        builder.Add(BackpackItemCategory.Reliquary,
        [
            new(AutoSortTokenKind.Level, SH.ViewPageBackpackSortLevel),
            new(AutoSortTokenKind.Quality, SH.ViewPageBackpackSortQuality),
            new(AutoSortTokenKind.EquipType, SH.ViewPageBackpackSortEquipType),
            new(AutoSortTokenKind.SetName, SH.ViewPageBackpackSortSetName),
            new(AutoSortTokenKind.Lock, SH.ViewPageBackpackSortLock),
            new(AutoSortTokenKind.Mark, SH.ViewPageBackpackSortMark),
        ]);

        ImmutableArray<AutoSortToken> commonTokens =
        [
            new(AutoSortTokenKind.Quality, SH.ViewPageBackpackSortQuality),
            new(AutoSortTokenKind.Name, SH.ViewPageBackpackSortName),
            new(AutoSortTokenKind.Count, SH.ViewPageBackpackSortCount),
        ];

        foreach (BackpackItemCategory cat in Enum.GetValues<BackpackItemCategory>())
        {
            if (cat is BackpackItemCategory.Weapon or BackpackItemCategory.Reliquary)
            {
                continue;
            }

            builder.Add(cat, commonTokens);
        }

        return builder.ToImmutable();
    }
}
