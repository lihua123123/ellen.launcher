// P1 只读数据源实现：读本地镜像用于双读比对，不注册到运行时 DI。
// HTTP 版（运行时接 ellen.wiki）属于 P3；接口已固定，两套 DTO 不混用。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class FileLauncherSnapshotSource : ILauncherSnapshotSource
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly string manifestPath;

    public FileLauncherSnapshotSource(string manifestPath)
    {
        this.manifestPath = manifestPath;
    }

    public async ValueTask<LauncherSnapshotReadResult> ReadAsync(CancellationToken token = default)
    {
        byte[] bytes = await File.ReadAllBytesAsync(manifestPath, token).ConfigureAwait(false);
        LauncherManifestDto manifest = JsonSerializer.Deserialize<LauncherManifestDto>(bytes, Options) ?? new();
        WikiSourceStamp stamp = WikiSourceStamp.FromEllenWiki("C1 /api/launcher", manifest.Digest, manifest.GeneratedAt);
        WikiDiagnosticsLog log = new();

        IReadOnlyList<string> files = manifest.Files.Select(f => f.Path).ToList();
        CurveValidation curves = CurveContract.Validate(manifest);
        if (curves.Alert is { } alert)
        {
            log.Add("curve-strict-alert", stamp, alert, CurveContract.ValidateMissingReason);
        }

        if (!manifest.Counts.ContainsKey("enemies"))
        {
            log.Add("dataset-missing", stamp, "manifest.counts 无 enemies；敌人 dataset 尚未进快照", "C1 快照不含敌人 dataset");
        }

        return new LauncherSnapshotReadResult
        {
            Manifest = manifest,
            Files = files,
            Stamp = stamp,
            Diagnostics = log.Records,
            Curves = curves,
        };
    }
}

public sealed class FileEnemyWebSource : IEnemyWebSource
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private readonly string root;
    private readonly string? digest;

    /// <param name="root">指向 ellen-wiki/src/data/enemies（含 index.json 与 items/）。</param>
    /// <param name="digest">C1 manifest digest，用于 source 戳；可空。</param>
    public FileEnemyWebSource(string root, string? digest = null)
    {
        this.root = root;
        this.digest = digest;
    }

    public async ValueTask<EnemyWebReadResult> ReadAsync(CancellationToken token = default)
    {
        WikiDiagnosticsLog log = new();
        WikiSourceStamp stamp = WikiSourceStamp.FromEllenWiki("C2 /api/enemies", digest, null);

        string indexPath = Path.Combine(root, "index.json");
        byte[] indexBytes = await File.ReadAllBytesAsync(indexPath, token).ConfigureAwait(false);
        EnemyIndexDocumentDto index;
        using (JsonDocument document = JsonDocument.Parse(indexBytes))
        {
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                // 线上 /api/enemies/index.json 当前是纯数组：categories / curves 都丢了。
                index = new EnemyIndexDocumentDto
                {
                    Enemies = JsonSerializer.Deserialize<List<EnemyIndexEntryDto>>(document.RootElement.GetRawText(), Options) ?? [],
                };
                log.Add("api-index-shape", stamp, "index 是纯数组，categories 与 curves 未随 API 暴露（G1）", "API 索引缺 curves/categories");
            }
            else
            {
                index = JsonSerializer.Deserialize<EnemyIndexDocumentDto>(indexBytes, Options) ?? new();
            }
        }

        Dictionary<string, EnemyItemDto> items = new(StringComparer.Ordinal);
        string itemsFolder = Path.Combine(root, "items");
        foreach (string file in Directory.EnumerateFiles(itemsFolder, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            string id = Path.GetFileNameWithoutExtension(file);
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(file, token).ConfigureAwait(false);
                EnemyItemDto? parsed = JsonSerializer.Deserialize<EnemyItemDto>(bytes, Options);
                if (parsed is null || !string.Equals(parsed.Id, id, StringComparison.Ordinal))
                {
                    log.Add("item-parse", stamp, $"{id}: 反序列化为空或 id 与文件名不一致", "item 解析失败");
                    continue;
                }

                items[id] = parsed;
            }
            catch (JsonException ex)
            {
                log.Add("item-parse", stamp, $"{id}: {ex.Message}", "item JSON 解析失败");
            }
        }

        return new EnemyWebReadResult
        {
            Index = index,
            Items = items,
            Stamp = stamp,
            Diagnostics = log.Records,
        };
    }
}
