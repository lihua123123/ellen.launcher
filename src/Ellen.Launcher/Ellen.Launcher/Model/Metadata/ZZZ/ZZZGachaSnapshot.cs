// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Metadata.ZZZ;

/// <summary>
/// 绝区零调频元数据的运行期快照：物品表 + 卡池档期表。
/// 缓存文件位于 <c>ServerCache/ZZZ/GachaMetadata.json</c>，由 <c>ZZZGachaMetadataService</c> 维护。
/// 随包的 <c>GachaItem.json</c> / <c>GachaBanner.json</c> 只作为离线兜底。
/// </summary>
public sealed class ZZZGachaSnapshot
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// HoYo.Gacha 元数据的最新版本哈希；随包兜底时为空。
    /// </summary>
    [JsonPropertyName("latest")]
    public string Latest { get; init; } = string.Empty;

    /// <summary>
    /// 最近一次成功刷新时间（UTC）。随包兜底为 <see cref="DateTimeOffset.MinValue"/>。
    /// </summary>
    [JsonPropertyName("fetched_at")]
    public DateTimeOffset FetchedAt { get; init; }

    [JsonPropertyName("items")]
    public List<ZZZGachaItemInfo> Items { get; init; } = [];

    [JsonPropertyName("banners")]
    public List<ZZZGachaBanner> Banners { get; init; } = [];
}
