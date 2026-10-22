// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Binding;

/// <summary>
/// 绑定接口返回的通用游戏角色（region 用原始字符串，兼容星穹铁道 / 绝区零的 prod_gf_cn 等区服）。
/// </summary>
public sealed class GameRolePayload
{
    [JsonPropertyName("game_biz")]
    public string GameBiz { get; set; } = string.Empty;

    [JsonPropertyName("region")]
    public string Region { get; set; } = string.Empty;

    [JsonPropertyName("game_uid")]
    public string GameUid { get; set; } = string.Empty;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("region_name")]
    public string RegionName { get; set; } = string.Empty;
}
