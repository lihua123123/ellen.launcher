// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Service.Backpack;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.Backpack;

public sealed class BackpackWeaponItemView : BackpackItemView
{
    public Weapon Weapon { get; private set; } = default!;

    public uint Level => Entity.Level;

    public uint RefinementRank => Entity.RefinementRank + 1;

    public bool IsLocked => Entity.IsLocked;

    public string WeaponTypeName => Weapon.WeaponType.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)!;

    public string LevelRefinementInfo => SH.FormatViewPageBackpackWeaponInfo(Level, RefinementRank);

    public static BackpackWeaponItemView Create(BackpackItem entity, BackpackServiceMetadataContext context, Weapon weapon)
    {
        return new()
        {
            Entity = entity,
            Category = BackpackItemCategory.Weapon,
            Weapon = weapon,
            Name = weapon.Name,
            Description = weapon.Description,
            TypeDescription = weapon.WeaponType.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)!,
            IconUri = EquipIconConverter.IconNameToUri(weapon.Icon),
            Quality = weapon.RankLevel,
        };
    }
}
