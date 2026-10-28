// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.SignIn;

/// <summary>
/// 签到卡片上展示的一件奖励物品（图标 + 数量）。
/// </summary>
public sealed class SignInAward
{
    private SignInAward(Award award)
    {
        Name = award.Name;
        Count = award.Count;
        Icon = Uri.TryCreate(award.Icon, UriKind.Absolute, out Uri? uri) ? uri : null;
    }

    public string Name { get; }

    public int Count { get; }

    public Uri? Icon { get; }

    public bool HasIcon
    {
        get => Icon is not null;
    }

    /// <summary>
    /// 「名称 ×数量」，数量小于等于 1 时只显示名称。
    /// </summary>
    public string DisplayName
    {
        get => Count > 1 ? $"{Name} ×{Count.ToString(CultureInfo.CurrentCulture)}" : Name;
    }

    public static SignInAward From(Award award)
    {
        return new(award);
    }
}
