// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Calculable;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public sealed class WeaponView : EquipView, ICalculableSource<ICalculableWeapon>
{
    public NameValue<string>? SubProperty { get; set; }

    public uint AffixLevelNumber { get; set; }

    public string AffixLevel { get => SH.FormatModelBindingAvatarPropertyWeaponAffix(AffixLevelNumber); }

    public string AffixName { get; set; } = default!;

    public string AffixDescription { get; set; } = default!;

    public ImmutableArray<bool> PromoteArray { get; set; }

    public WeaponId Id { get; set; }

    public uint LevelNumber { get; set; }

    public uint MaxLevel { get => Model.Metadata.Weapon.Weapon.GetMaxLevelByQuality(Quality); }

    public WeaponType WeaponType { get; set; }

    public PromoteLevel PromoteLevel { get; set; }

    public ICalculableWeapon ToCalculable()
    {
        return CalculableWeapon.From(this);
    }
}