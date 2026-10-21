// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public class ReliquaryComposedSubProperty : ReliquarySubProperty
{
    public ReliquaryComposedSubProperty(FightProperty type, string value, uint enhancedCount, double enhancementCount, string? name = null)
        : base(type, value, name)
    {
        Type = type;
        EnhancedCount = enhancedCount;
        EnhancementCount = enhancementCount;
    }

    public uint EnhancedCount { get; set; }

    /// <summary>
    /// 命中数：该副词条被强化命中的次数，不含它自身出现的那一次，显示成「+N」。
    /// 一次都没强化到时（+0）不显示，但中间列仍然占着位置。
    /// </summary>
    public string EnhancedCountText => EnhancedCount > 1 ? $"+{EnhancedCount - 1}" : string.Empty;

    /// <summary>
    /// 强化数：词条数值相对「一次满档强化」的倍数，换算规则见
    /// <see cref="Service.AvatarInfo.Factory.ReliquaryEnhancementCalculator"/>。
    /// </summary>
    public double EnhancementCount { get; set; }

    /// <summary>
    /// 词条数显示格式。原神保留一位小数，绝区零使用两位小数。
    /// </summary>
    public string EnhancementCountFormat { get; set; } = "0.0";

    public string EnhancementCountText => EnhancementCount > 0 ? EnhancementCount.ToString(EnhancementCountFormat, CultureInfo.CurrentCulture) : string.Empty;

    public FightProperty Type { get; }
}