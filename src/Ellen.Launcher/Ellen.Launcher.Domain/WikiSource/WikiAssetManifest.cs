// P4：敌人图片 assets manifest 契约 + 全量校验/离线解析支持。
//
// 上游尚未提供 manifest，本文件先冻结客户端侧契约（schema=ellen-enemy-assets-v1）：
//   { schema, generatedAt, count, bytes, files: [{ id, file, size, sha256, url? }] }
// 客户端行为：
//   - manifest 缺失 → 显式降级为「在线 URL + 按需缓存」，不宣称离线（P3 行为）；
//   - manifest 存在 → 按 id/file 校验本地缓存，sha256 不符即重下；全量命中时离线可解析；
//   - orphan（不在 items 引用里）不得出现在 manifest，也不进缓存。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class EnemyAssetFileDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("file")] public string File { get; set; } = string.Empty;
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;

    /// <summary>可选：上游给的绝对/相对 URL；缺省时按 /images/enemies/&lt;id&gt;/&lt;file&gt; 拼。</summary>
    [JsonPropertyName("url")] public string? Url { get; set; }

    /// <summary>可选缩略图声明（P6 随包据此找文件；缺失表示原图本身已是缩略尺寸）。</summary>
    [JsonPropertyName("thumb")] public EnemyAssetThumbDto? Thumb { get; set; }

    public string Key => $"{Id}/{File}";
}

/// <summary>缩略图声明：文件名 + 尺寸 + 体积 + sha256。客户端只用来定位与校验，不重新编码。</summary>
public sealed class EnemyAssetThumbDto
{
    [JsonPropertyName("file")] public string File { get; set; } = string.Empty;
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("width")] public int Width { get; set; }
    [JsonPropertyName("height")] public int Height { get; set; }
    [JsonPropertyName("format")] public string? Format { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = string.Empty;
}

/// <summary>
/// 许可块（A4）。<see cref="Redistributable"/> 决定图片能否打进安装包；
/// <see cref="Attribution"/> 必须在关于页/诊断页展示（P5-offline-package §3 验收 #7）。
/// </summary>
public sealed class EnemyAssetLicenseDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("redistributable")] public bool Redistributable { get; set; }
    [JsonPropertyName("attribution")] public string? Attribution { get; set; }
    [JsonPropertyName("approvedBy")] public string? ApprovedBy { get; set; }
    [JsonPropertyName("approvedAt")] public string? ApprovedAt { get; set; }
    [JsonPropertyName("approvalSource")] public string? ApprovalSource { get; set; }
    [JsonPropertyName("approvalBasis")] public string? ApprovalBasis { get; set; }
}

public sealed class EnemyAssetManifestDto
{
    [JsonPropertyName("schema")] public string? Schema { get; set; }
    [JsonPropertyName("generatedAt")] public string? GeneratedAt { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("bytes")] public long Bytes { get; set; }
    [JsonPropertyName("license")] public EnemyAssetLicenseDto? License { get; set; }
    [JsonPropertyName("files")] public List<EnemyAssetFileDto> Files { get; set; } = [];
}

public sealed class WikiAssetManifest
{
    public const string ExpectedSchema = "ellen-enemy-assets-v1";

    private readonly Dictionary<string, EnemyAssetFileDto> byKey;

    private WikiAssetManifest(EnemyAssetManifestDto dto)
    {
        Dto = dto;
        byKey = dto.Files
            .Where(f => !string.IsNullOrEmpty(f.Id) && !string.IsNullOrEmpty(f.File))
            .GroupBy(f => f.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    public EnemyAssetManifestDto Dto { get; }

    public int Count => byKey.Count;

    public long Bytes => Dto.Bytes > 0 ? Dto.Bytes : Dto.Files.Sum(f => f.Size);

    public IReadOnlyCollection<string> Keys => byKey.Keys;

    /// <summary>许可块（可能为空：早期清单没有 license，按「不可随包」理解）。</summary>
    public EnemyAssetLicenseDto? License => Dto.License;

    /// <summary>清单声明了缩略图的条目数。</summary>
    public int DeclaredThumbCount => Dto.Files.Count(f => f.Thumb is not null);

    public bool TryGet(string id, string file, out EnemyAssetFileDto entry)
        => byKey.TryGetValue($"{id}/{file}", out entry!);

    /// <summary>解析 JSON；schema 不符视为不可用（返回 null，由调用方显式降级）。</summary>
    public static WikiAssetManifest? Parse(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        try
        {
            EnemyAssetManifestDto? dto = JsonSerializer.Deserialize<EnemyAssetManifestDto>(bytes);
            if (dto?.Files is not { Count: > 0 })
            {
                return null;
            }

            // schema 缺失时宽容接受（上游早期版本），但显式记录的 schema 必须匹配。
            if (!string.IsNullOrEmpty(dto.Schema)
                && !string.Equals(dto.Schema, ExpectedSchema, StringComparison.Ordinal))
            {
                return null;
            }

            return new WikiAssetManifest(dto);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static WikiAssetManifest? LoadFile(string path)
        => File.Exists(path) ? Parse(File.ReadAllBytes(path)) : null;

    /// <summary>校验：manifest 内每条都能被 items 引用（orphan 不得进 manifest），且 sha 长度正确。</summary>
    public WikiAssetManifestValidation Validate(IEnumerable<string> allowedKeys)
    {
        HashSet<string> allowed = new(allowedKeys, StringComparer.Ordinal);
        List<string> orphans = [];
        List<string> badHash = [];
        foreach (EnemyAssetFileDto f in Dto.Files)
        {
            if (!allowed.Contains(f.Key))
            {
                orphans.Add(f.Key);
            }

            if (f.Sha256.Length != 64)
            {
                badHash.Add(f.Key);
            }
        }

        return new WikiAssetManifestValidation
        {
            Total = Dto.Files.Count,
            OrphanKeys = orphans,
            BadHashKeys = badHash,
            SchemaOk = string.IsNullOrEmpty(Dto.Schema) || string.Equals(Dto.Schema, ExpectedSchema, StringComparison.Ordinal),
        };
    }
}

public sealed class WikiAssetManifestValidation
{
    public int Total { get; init; }
    public IReadOnlyList<string> OrphanKeys { get; init; } = [];
    public IReadOnlyList<string> BadHashKeys { get; init; } = [];
    public bool SchemaOk { get; init; }
    public bool Pass => OrphanKeys.Count == 0 && BadHashKeys.Count == 0 && SchemaOk;
}

public sealed class WikiImageSyncResult
{
    public int Total { get; init; }
    public int AlreadyCached { get; init; }
    public int Downloaded { get; init; }
    public int Failed { get; init; }
    public int HashRejected { get; init; }
    public long Bytes { get; init; }
    public double ElapsedMs { get; init; }
    public bool Pass => Failed == 0 && HashRejected == 0;
}
