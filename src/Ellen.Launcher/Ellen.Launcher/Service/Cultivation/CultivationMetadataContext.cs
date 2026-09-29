// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Cultivation;

public class CultivationMetadataContext : ICultivationMetadataContext
{
    public ImmutableArray<Material> Materials { get; set; } = default!;

    public ImmutableArray<GachaEvent> GachaEvents { get; set; } = default!;

    public ImmutableDictionary<MaterialId, Material> IdMaterialMap { get; set; } = default!;

    public ImmutableDictionary<AvatarId, Model.Metadata.Avatar.Avatar> IdAvatarMap { get; set; } = default!;

    public ImmutableDictionary<WeaponId, Model.Metadata.Weapon.Weapon> IdWeaponMap { get; set; } = default!;

    public ImmutableDictionary<MaterialId, Combine> ResultMaterialIdCombineMap { get; set; } = default!;

    public Item GetAvatarItem(AvatarId avatarId)
    {
        return this.GetAvatar(avatarId).GetOrCreateItem();
    }

    public Item GetWeaponItem(WeaponId weaponId)
    {
        return this.GetWeapon(weaponId).GetOrCreateItem();
    }
}