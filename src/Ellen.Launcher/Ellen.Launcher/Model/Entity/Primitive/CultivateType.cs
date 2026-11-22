// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Entity.Primitive;

[ExtendedEnum]
public enum CultivateType
{
    None,

    [LocalizationKey(nameof(SH.ModelEntityPrimitiveCultivateTypeAvatarAndSkill))]
    AvatarAndSkill,

    [LocalizationKey(nameof(SH.ModelEntityPrimitiveCultivateTypeWeapon))]
    Weapon,

    [LocalizationKey(nameof(SH.ModelEntityPrimitiveCultivateTypeFurniture))]
    Furniture,

    /// <summary>
    /// 自定义材料计划：用户自行选择角色养成材料及数量。
    /// </summary>
    [LocalizationKey(nameof(SH.ModelEntityPrimitiveCultivateTypeCustomAvatarMaterial))]
    CustomAvatarMaterial,

    /// <summary>
    /// 自定义材料计划：用户自行选择武器养成材料及数量。
    /// </summary>
    [LocalizationKey(nameof(SH.ModelEntityPrimitiveCultivateTypeCustomWeaponMaterial))]
    CustomWeaponMaterial,
}