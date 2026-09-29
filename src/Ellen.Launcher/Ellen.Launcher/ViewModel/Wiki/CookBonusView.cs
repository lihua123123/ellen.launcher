// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Wiki;

public sealed class CookBonusView
{
    public Material OriginItem { get; set; } = default!;

    public Material Item { get; set; } = default!;

    public static CookBonusView? Create(CookBonus? cookBonus, ImmutableDictionary<MaterialId, Material> idMaterialMap)
    {
        if (cookBonus is null)
        {
            return null;
        }

        CookBonusView view = new()
        {
            OriginItem = idMaterialMap[cookBonus.OriginItemId],
            Item = idMaterialMap[cookBonus.ItemId],
        };

        return view;
    }
}