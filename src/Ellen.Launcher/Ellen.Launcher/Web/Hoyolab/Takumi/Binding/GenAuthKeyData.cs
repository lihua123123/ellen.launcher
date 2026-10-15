// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Globalization;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Binding;

public sealed class GenAuthKeyData
{
    public GenAuthKeyData(string authAppId, string gameBiz, PlayerUid uid)
    {
        AuthAppId = authAppId;
        GameBiz = gameBiz;
        GameUid = int.Parse(uid.Value, CultureInfo.InvariantCulture);
        Region = uid.Region;
    }

    private GenAuthKeyData(string authAppId, string gameBiz, string gameUid, string region)
    {
        AuthAppId = authAppId;
        GameBiz = gameBiz;
        GameUid = int.Parse(gameUid, CultureInfo.InvariantCulture);
        Region = Region.UnsafeFromRegionString(region);
    }

    [JsonPropertyName("auth_appid")]
    public string AuthAppId { get; set; }

    [JsonPropertyName("game_biz")]
    public string GameBiz { get; set; }

    [JsonPropertyName("game_uid")]
    public int GameUid { get; set; }

    [JsonPropertyName("region")]
    public Region Region { get; set; }

    public static GenAuthKeyData CreateForWebViewGacha(PlayerUid uid)
    {
        return new("webview_gacha", "hk4e_cn", uid);
    }

    /// <summary>
    /// 绝区零调频记录（webview_gacha），国服 game_biz 为 nap_cn。
    /// 绝区零的 uid 与区服不满足原神的格式约束，因此直接透传原始字符串。
    /// </summary>
    public static GenAuthKeyData CreateForZZZWebViewGacha(string gameUid, string region)
    {
        return new("webview_gacha", "nap_cn", gameUid, string.IsNullOrEmpty(region) ? "prod_gf_cn" : region);
    }
}