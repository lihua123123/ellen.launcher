// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;

namespace Ellen.Launcher.Model.Metadata.ZZZ;

/// <summary>
/// 绝区零「我的角色」本地缓存：队伍角色列表、每个角色的详情，以及 Wiki 影像/技能图标地址。
/// </summary>
public sealed class ZZZAvatarCache
{
    public string GameUid { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }

    public List<ZZZAvatarBasic> Agents { get; set; } = [];

    public Dictionary<int, string> DetailsJson { get; set; } = [];

    public Dictionary<string, ZZZWikiAssetCache> WikiAssets { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ZZZWikiAssetCache
{
    public string[] CinemaImages { get; set; } = [];

    public string[] SkillIcons { get; set; } = [];
}
