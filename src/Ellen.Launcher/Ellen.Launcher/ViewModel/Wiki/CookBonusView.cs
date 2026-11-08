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

        /* 新角色可能引用当前数据源里还没有的材料 id，缺了就整块不显示，别让页面炸掉 */
        if (!idMaterialMap.TryGetValue(cookBonus.OriginItemId, out Material? originItem)
            || !idMaterialMap.TryGetValue(cookBonus.ItemId, out Material? item))
        {
            return null;
        }

        CookBonusView view = new()
        {
            OriginItem = originItem,
            Item = item,
        };

        return view;
    }
}