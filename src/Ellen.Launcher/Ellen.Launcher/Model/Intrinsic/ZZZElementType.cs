// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Intrinsic;

/// <summary>
/// 绝区零属性。
/// 数值与米游社战绩 <c>element_type</c> 一致：200 物理、201 火、202 冰、203 电、
/// 204 风、205 以太、206 烈霜、207 玄墨、208 凛刃、209 流明。
/// </summary>
[ExtendedEnum]
public enum ZZZElementType
{
    None = 0,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementPhysical))]
    Physical = 200,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementFire))]
    Fire = 201,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementIce))]
    Ice = 202,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementElectric))]
    Electric = 203,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementWind))]
    Wind = 204,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementEther))]
    Ether = 205,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementFrost))]
    Frost = 206,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementInk))]
    Ink = 207,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementRime))]
    Rime = 208,

    [LocalizationKey(nameof(SH.ModelIntrinsicZZZElementLumen))]
    Lumen = 209,
}
