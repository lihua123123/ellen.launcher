// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Metadata.ZZZ;

/// <summary>
/// 绝区零调频档期快照（来自 HoYo.Gacha 公共元数据 + 米游社官方公告）。
/// 每个条目对应一个独立卡池：类型、起止时间、卡池名、S 级 UP 物品 id、版本号、卡池图。
/// 同期开放的两个频段会拆成两条记录（各自带自己的卡池图）。
/// </summary>
public sealed class ZZZGachaBannerDataset
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("index")]
    public string Index { get; init; } = string.Empty;

    [JsonPropertyName("latest")]
    public string Latest { get; init; } = string.Empty;

    [JsonPropertyName("banners")]
    public List<ZZZGachaBanner> Banners { get; init; } = [];
}

public sealed class ZZZGachaBanner
{
    /// <summary>
    /// 调频类型：1 常驻 / 2 独家 / 3 音擎 / 5 邦布 / 102 独家重映 / 103 音擎回响
    /// </summary>
    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("start")]
    public DateTimeOffset Start { get; init; }

    [JsonPropertyName("end")]
    public DateTimeOffset End { get; init; }

    /// <summary>
    /// 官方公告里的卡池名（例如「砺世澄沧」去掉引号）。公告缺失时为空，界面退回显示频段名。
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// S 级 UP 物品 id（拆分成独立卡池后只有一个）
    /// </summary>
    [JsonPropertyName("up")]
    public List<uint> Up { get; init; } = [];

    /// <summary>
    /// A 级 UP 物品 id（部分版本官方数据为空）
    /// </summary>
    [JsonPropertyName("up_purple")]
    public List<uint> UpPurple { get; init; } = [];

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// 官方公告中的卡池图 URL（可能为空，例如尚未公布的新版本）。
    /// 来源：米游社官方「X版本限时频段」与「XXX调频说明」公告。
    /// </summary>
    [JsonPropertyName("image")]
    public string Image { get; init; } = string.Empty;
}
