// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.Backpack;

public sealed class BackpackReliquarySubStatView
{
    public static readonly BackpackReliquarySubStatView Empty = new() { HasValue = false };

    public FightProperty FightProp { get; init; }

    public float Value { get; init; }

    public uint EnhancedCount { get; init; }

    public bool HasValue { get; init; } = true;

    /// <summary>
    /// 命中数：该副词条被强化命中的次数，不含它自身出现的那一次，显示成「+N」。
    /// 一次都没强化到时（+0）不显示，但中间列仍然占着位置。
    /// </summary>
    public string EnhancedCountText => HasValue && EnhancedCount > 1 ? $"+{EnhancedCount - 1}" : string.Empty;

    public string DisplayName => HasValue ? FightProp.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture)! : string.Empty;

    public string DisplayValue => HasValue
        ? (FightProp.IsFightPropPercent()
            ? Value.ToString("P1", CultureInfo.CurrentCulture)
            : Value.ToString("F0", CultureInfo.CurrentCulture))
        : string.Empty;
}
