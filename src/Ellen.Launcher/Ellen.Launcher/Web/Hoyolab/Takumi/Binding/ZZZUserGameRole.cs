// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Binding;

/// <summary>
/// 绝区零游戏角色。
/// 不能复用 <see cref="UserGameRole"/>：其 Region 是 <see cref="Region"/> 结构体，
/// 反序列化时只接受原神区服字符串（cn_gf01 / os_usa 等），
/// 遇到绝区零的 prod_gf_cn 会抛异常，导致请求整体失败。
/// </summary>
public sealed class ZZZUserGameRole
{
    [JsonPropertyName("game_biz")]
    public string GameBiz { get; set; } = default!;

    /// <summary>
    /// 区服，例如 prod_gf_cn
    /// </summary>
    [JsonPropertyName("region")]
    public string Region { get; set; } = default!;

    [JsonPropertyName("game_uid")]
    public string GameUid { get; set; } = default!;

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = default!;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("region_name")]
    public string RegionName { get; set; } = default!;
}
