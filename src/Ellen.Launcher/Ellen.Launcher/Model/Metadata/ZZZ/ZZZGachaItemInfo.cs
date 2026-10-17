// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Model.Metadata.ZZZ;

/// <summary>
/// 绝区零调频物品元数据（图标、名称、稀有度、类型）。
/// 数据由 <c>tools/ZZZGachaInfoGen/generate.py</c> 从 Starward 公共元数据镜像生成，
/// 随应用发布为 <c>Resource/Metadata/ZZZ/GachaItem.json</c>，运行期不发起元数据请求。
/// </summary>
public sealed class ZZZGachaItemInfo
{
    [JsonPropertyName("id")]
    public uint Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 图标地址。图片本体不随包发布，由 <c>CachedImage</c> 运行期下载并缓存。
    /// </summary>
    [JsonPropertyName("icon")]
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// 绝区零稀有度：4 = S，3 = A，2 = B
    /// </summary>
    [JsonPropertyName("rarity")]
    public int Rarity { get; init; }

    /// <summary>
    /// <c>agent</c> / <c>wengine</c> / <c>bangboo</c>
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonIgnore]
    public QualityType Quality
    {
        get => Rarity switch
        {
            4 => QualityType.QUALITY_ORANGE,
            3 => QualityType.QUALITY_PURPLE,
            2 => QualityType.QUALITY_BLUE,
            _ => QualityType.QUALITY_NONE,
        };
    }

    [JsonIgnore]
    public Uri? IconUri => string.IsNullOrEmpty(Icon) ? null : Icon.ToUri();

    [JsonIgnore]
    public bool IsAgent => Type is "agent";

    [JsonIgnore]
    public bool IsBangboo => Type is "bangboo";
}

/// <summary>
/// <c>Resource/Metadata/ZZZ/GachaItem.json</c> 的根节点。
/// </summary>
public sealed class ZZZGachaItemInfoDataset
{
    [JsonPropertyName("source")]
    public string Source { get; init; } = string.Empty;

    [JsonPropertyName("game")]
    public string Game { get; init; } = string.Empty;

    [JsonPropertyName("lang")]
    public string Lang { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public List<ZZZGachaItemInfo> Items { get; init; } = [];
}
