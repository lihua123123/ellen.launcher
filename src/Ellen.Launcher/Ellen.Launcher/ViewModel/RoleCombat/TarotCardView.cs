// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat;

namespace Ellen.Launcher.ViewModel.RoleCombat;

/// <summary>
/// 「月谕圣牌」页中的一张牌。
/// </summary>
public sealed class TarotCardView
{
    /// <summary>
    /// 未解锁的牌在接口里 <c>icon</c> 是空字符串，这里换成灰底的圣牌图标作为占位。
    /// </summary>
    private static readonly Uri LockedIcon = new("ms-appx:///Resource/Icon/UI_Icon_RoleCombat_Reward_Tarot_05.png");

    public required string Name { get; init; }

    public required bool IsUnlock { get; init; }

    public required int UnlockNumber { get; init; }

    public required Uri Icon { get; init; }

    /// <summary>
    /// 已解锁且只有一张时留空，此时界面上的那一行会自然塌陷。
    /// </summary>
    public required string StatusText { get; init; }

    public double CardOpacity { get => IsUnlock ? 1D : 0.4D; }

    public static TarotCardView Create(TarotCard card)
    {
        return new()
        {
            Name = card.Name,
            IsUnlock = card.IsUnlock,
            UnlockNumber = card.UnlockNumber,
            Icon = card.Icon.IsAbsoluteUri ? card.Icon : LockedIcon,
            StatusText = card.IsUnlock
                ? (card.UnlockNumber > 1 ? $"×{card.UnlockNumber}" : string.Empty)
                : SH.ViewTarotCardLocked,
        };
    }
}
