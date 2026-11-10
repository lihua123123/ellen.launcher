// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Specialized;
using System.Web;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;

/// <summary>
/// 支持米游社游戏签到的游戏。
/// </summary>
public enum SignInGame
{
    Genshin,
    StarRail,
    ZenlessZoneZero,
}

/// <summary>
/// 一个可签到的游戏角色。
/// 与 <see cref="Ellen.Launcher.Web.Hoyolab.Takumi.Binding.UserGameRole"/> 不同，这里的区服用原始字符串保存（如 prod_gf_cn），
/// 因此可以同时容纳原神 / 星穹铁道 / 绝区零的区服。
/// </summary>
public sealed class SignInGameRole
{
    public required SignInGame Game { get; init; }

    /// <summary>
    /// 游戏内 UID。
    /// </summary>
    public required string Uid { get; init; }

    /// <summary>
    /// 区服，例如 cn_gf01 / prod_gf_cn。
    /// </summary>
    public required string Region { get; init; }

    /// <summary>
    /// hk4e_cn / hkrpg_cn / nap_cn / hk4e_global ...
    /// </summary>
    public string GameBiz { get; init; } = string.Empty;

    public string? Nickname { get; init; }

    public int Level { get; init; }

    /// <summary>
    /// 用于接口请求头的 x-rpc-signgame 值。
    /// </summary>
    public string SignGameHeader
    {
        get => Game switch
        {
            SignInGame.Genshin => "hk4e",
            SignInGame.StarRail => "hkrpg",
            SignInGame.ZenlessZoneZero => "zzz",
            _ => "hk4e",
        };
    }

    public string GameName
    {
        get => Game switch
        {
            SignInGame.Genshin => SH.ServiceSignInGameGenshin,
            SignInGame.StarRail => SH.ServiceSignInGameStarRail,
            SignInGame.ZenlessZoneZero => SH.ServiceSignInGameZenlessZoneZero,
            _ => string.Empty,
        };
    }

    /// <summary>
    /// 「昵称 | 游戏名」
    /// </summary>
    public string DisplayName
    {
        get => string.IsNullOrEmpty(Nickname)
            ? $"{GameName} | {Uid}"
            : $"{Nickname} | {GameName}";
    }

    public override string ToString()
    {
        return $"{DisplayName} ({Uid})";
    }
}

public static class SignInGameRoleExtension
{
    public static string ToUidRegionQueryString(this SignInGameRole role)
    {
        NameValueCollection collection = HttpUtility.ParseQueryString(string.Empty);
        collection.Set("uid", role.Uid);
        collection.Set("region", role.Region);

        string? query = collection.ToString();
        ArgumentNullException.ThrowIfNull(query);
        return query;
    }
}
