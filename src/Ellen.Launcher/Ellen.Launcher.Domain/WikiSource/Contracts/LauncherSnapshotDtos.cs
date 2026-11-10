// C1 契约 DTO：/api/launcher/manifest.json + launcher-metadata.zip 快照。
// Snap.Metadata 兼容格式（PascalCase + 枚举数字），与 C2 敌人网页版 DTO 严格分离。

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ellen.Launcher.Domain.WikiSource.Contracts;

public sealed class LauncherManifestDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    [JsonPropertyName("locale")]
    public string Locale { get; set; } = string.Empty;

    [JsonPropertyName("counts")]
    public Dictionary<string, int> Counts { get; set; } = new(System.StringComparer.Ordinal);

    [JsonPropertyName("archive")]
    public LauncherArchiveDto? Archive { get; set; }

    [JsonPropertyName("files")]
    public List<LauncherFileDto> Files { get; set; } = [];

    /// <summary>P4 需求：分数据集 digest，避免 753 items + 图片导致增量退化成全量。</summary>
    [JsonPropertyName("datasets")]
    public Dictionary<string, WikiDatasetDto>? Datasets { get; set; }
}

public sealed class LauncherArchiveDto
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("entries")]
    public int Entries { get; set; }
}

public sealed class LauncherFileDto
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class WikiDatasetDto
{
    [JsonPropertyName("digest")]
    public string Digest { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

/// <summary>MonsterCurve.json 的一行：[{Level, Curves:[{Type, Value}]}]。Type 12 = GROW_CURVE_DEFENSE。</summary>
public sealed class LauncherCurveRowDto
{
    [JsonPropertyName("Level")]
    public int Level { get; set; }

    [JsonPropertyName("Curves")]
    public List<LauncherCurveCellDto> Curves { get; set; } = [];
}

public sealed class LauncherCurveCellDto
{
    [JsonPropertyName("Type")]
    public int Type { get; set; }

    [JsonPropertyName("Value")]
    public double Value { get; set; }
}
