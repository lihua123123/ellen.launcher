// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data.Converter;
using Ellen.Launcher.Web.Endpoint.Upstream;
using System.Collections.Frozen;

namespace Ellen.Launcher.Model.Metadata.Converter;

public sealed partial class WeaponTypeIconConverter : ValueConverter<WeaponType, Uri>
{
    private static readonly FrozenDictionary<string, WeaponType> LocalizedNameToWeaponType = WinRTAdaptive.ToFrozenDictionary(
    [
        KeyValuePair.Create(SH.ModelIntrinsicWeaponTypeSwordOneHand, WeaponType.WEAPON_SWORD_ONE_HAND),
        KeyValuePair.Create(SH.ModelIntrinsicWeaponTypeBow, WeaponType.WEAPON_BOW),
        KeyValuePair.Create(SH.ModelIntrinsicWeaponTypePole, WeaponType.WEAPON_POLE),
        KeyValuePair.Create(SH.ModelIntrinsicWeaponTypeClaymore, WeaponType.WEAPON_CLAYMORE),
        KeyValuePair.Create(SH.ModelIntrinsicWeaponTypeCatalyst, WeaponType.WEAPON_CATALYST),
    ]);

    public static Uri WeaponTypeNameToIconUri(string weaponTypeName)
    {
        return WeaponTypeToIconUri(LocalizedNameToWeaponType.GetValueOrDefault(weaponTypeName));
    }

    public static Uri WeaponTypeToIconUri(WeaponType type)
    {
        string weapon = type switch
        {
            WeaponType.WEAPON_SWORD_ONE_HAND => "01",
            WeaponType.WEAPON_BOW => "02",
            WeaponType.WEAPON_POLE => "03",
            WeaponType.WEAPON_CLAYMORE => "04",
            WeaponType.WEAPON_CATALYST => "Catalyst_MD",
            _ => throw EllenException.NotSupported(),
        };

        return StaticResourcesEndpoints.StaticRaw("Skill", $"Skill_A_{weapon}.png").ToUri();
    }

    public override Uri Convert(WeaponType from)
    {
        return WeaponTypeToIconUri(from);
    }
}