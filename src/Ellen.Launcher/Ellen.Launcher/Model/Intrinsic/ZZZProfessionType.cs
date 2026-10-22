// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Intrinsic;

/// <summary>
/// 绝区零代理人职业（特性）。
/// 数值与米游社战绩 <c>avatar_profession</c> 一致。
/// </summary>
[ExtendedEnum]
public enum ZZZProfessionType
{
    None = 0,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZProfessionAttack))]
    Attack = 1,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZProfessionStun))]
    Stun = 2,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZProfessionAnomaly))]
    Anomaly = 3,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZProfessionSupport))]
    Support = 4,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZProfessionDefense))]
    Defense = 5,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZProfessionRupture))]
    Rupture = 6,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZProfessionVanguard))]
    Vanguard = 7,
}
