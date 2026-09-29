// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;
using InGameWeapon = Ellen.Launcher.Service.Yae.PlayerStore.Weapon;

namespace Ellen.Launcher.Model.InterChange.Inventory;

// ReSharper disable once InconsistentNaming
public sealed class UIIFWeapon
{
    [JsonPropertyName("level")]
    public Level Level { get; init; }

    [JsonPropertyName("promoteLevel")]
    public Level PromoteLevel { get; init; }

    [JsonPropertyName("affixMap")]
    public ImmutableDictionary<EquipAffixId, WeaponAffixLevel>? AffixMap { get; init; }

    public static UIIFWeapon FromInGameWeapon(InGameWeapon weapon)
    {
        return new()
        {
            Level = weapon.Level,
            PromoteLevel = weapon.PromoteLevel,
            AffixMap = weapon.AffixMap?.ToImmutableDictionary<KeyValuePair<uint, uint>, EquipAffixId, WeaponAffixLevel>(kv => kv.Key, kv => kv.Value),
        };
    }
}