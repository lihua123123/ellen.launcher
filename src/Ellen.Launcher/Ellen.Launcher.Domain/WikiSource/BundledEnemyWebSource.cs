// P5 客户端 753 条目 JSON 随包：离线首屏数据源（方案 B 地基）。
// 包裹 HTTP 源：ReadAsync 走线上（同步/回填 HTTP 缓存）；ReadCachedAsync 走离线优先——
// 先读 HTTP 缓存，空则读随包 753，再空才让上层回退到 legacy 722（显式，不白屏）。
// 随包缺失时显式记录诊断，不静默丢数据。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

/// <summary>
/// 离线优先的敌人数据源装饰器。随包目录约定：<c>index.json</c> + 扁平的 <c>&lt;slug&gt;.json</c>
/// （文件名即条目 id，与线上 /api/enemies/&lt;slug&gt;.json 一致）。
/// </summary>
public sealed class BundledEnemyWebSource : IEnemyWebSource, ICachedEnemyWebSource, IWikiSourceStatsProvider
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 把随包内容种入 HTTP 缓存（754 条，约 30 MB）默认关闭：
    /// 随包文件随安装包长期存在，再复制一份到缓存纯属重复；实测种缓存要 2.6–2.9s，
    /// 而种完之后下次启动读缓存（~880ms）反而比直接读随包（~700ms）更慢。
    /// 设 <c>ELLEN_WIKI_BUNDLE_SEED=1</c> 可恢复旧行为（仅用于排查缓存相关问题时对齐数据）。
    /// </summary>
    public const string SeedEnvironmentVariable = "ELLEN_WIKI_BUNDLE_SEED";

    private readonly string bundleRoot;
    private readonly IEnemyWebSource inner;
    private readonly WikiCacheStore? cache;
    private readonly WikiDiagnosticsLog? log;

    public BundledEnemyWebSource(string bundleRoot, IEnemyWebSource inner, WikiCacheStore? cache = null, WikiDiagnosticsLog? log = null)
    {
        this.bundleRoot = bundleRoot ?? string.Empty;
        this.inner = inner;
        this.cache = cache;
        this.log = log;
    }

    /// <summary>是否启用「读随包后写缓存」。默认关闭，见 <see cref="SeedEnvironmentVariable"/>。</summary>
    public bool SeedCacheEnabled { get; init; }
        = string.Equals(Environment.GetEnvironmentVariable(SeedEnvironmentVariable), "1", StringComparison.Ordinal);

    public WikiCacheStats Stats => (inner as IWikiSourceStatsProvider)?.Stats ?? new WikiCacheStats();

    /// <summary>线上路径：直接委托内层 HTTP 源（同步并回填缓存）。</summary>
    public ValueTask<EnemyWebReadResult> ReadAsync(CancellationToken token = default)
        => inner.ReadAsync(token);

    /// <summary>
    /// 离线优先：① HTTP 缓存有数直接返回；② 缓存空读随包 753；③ 二者皆空返回空（上层按 legacy 722 处理）。
    /// 默认不把随包内容写回缓存，见 <see cref="SeedCacheEnabled"/>。
    /// </summary>
    public async ValueTask<EnemyWebReadResult> ReadCachedAsync(CancellationToken token = default)
    {
        EnemyWebReadResult innerCached = inner is ICachedEnemyWebSource cachedSource
            ? await cachedSource.ReadCachedAsync(token).ConfigureAwait(false)
            : await inner.ReadAsync(token).ConfigureAwait(false);

        if (innerCached.Items.Count > 0)
        {
            return innerCached;
        }

        if (!string.IsNullOrEmpty(bundleRoot) && Directory.Exists(bundleRoot))
        {
            (EnemyWebReadResult bundled, Dictionary<string, byte[]> raw) = ReadBundle(token, SeedCacheEnabled);
            if (bundled.Items.Count > 0)
            {
                if (SeedCacheEnabled)
                {
                    await SeedCacheAsync(bundled, raw, token).ConfigureAwait(false);
                }

                return bundled;
            }

            log?.Add("bundle-empty", WikiSourceStamp.FromBundledWeb753("随包敌人数据为空"),
                $"{bundleRoot} 存在但无有效条目，回退到缓存/legacy", "随包 753 缺失");
        }
        else if (!string.IsNullOrEmpty(bundleRoot))
        {
            log?.Add("bundle-missing", WikiSourceStamp.FromBundledWeb753("随包目录缺失"),
                $"{bundleRoot} 不存在，离线首屏回退 legacy", "随包 753 未随包");
        }

        return innerCached;
    }

    /// <param name="collectRaw">
    /// 是否把原始字节一并留在内存里（只给「种缓存」用）。关闭时省掉 753 个 byte[] 的复制（约 30 MB 分配）。
    /// </param>
    private (EnemyWebReadResult Result, Dictionary<string, byte[]> Raw) ReadBundle(CancellationToken token, bool collectRaw)
    {
        WikiSourceStamp stamp = WikiSourceStamp.FromBundledWeb753("随包 753 离线基线");
        Dictionary<string, byte[]> raw = new(StringComparer.Ordinal);
        List<WikiDiagnostic> diagnostics = [.. (log?.Records ?? [])];

        // 合并包优先：一次读取 + 一次反序列化（~2.3 MiB），比逐个打开 753 个文件快 ~4 倍。
        // 需要 raw（种缓存）时走逐条路径，因为合并包里没有原始字节。
        if (!collectRaw
            && TryReadMerged(Path.Combine(bundleRoot, "bundled-all.json"), stamp, diagnostics, token) is { } merged)
        {
            return (merged, raw);
        }

        string indexPath = Path.Combine(bundleRoot, "index.json");
        if (!File.Exists(indexPath))
        {
            log?.Add("bundle-no-index", stamp, $"{indexPath} 缺失", "随包 753 不完整");
            return (new EnemyWebReadResult { Stamp = stamp, Diagnostics = diagnostics }, raw);
        }

        byte[] indexBytes = File.ReadAllBytes(indexPath);
        if (collectRaw)
        {
            raw["enemy-index.json"] = indexBytes;
        }

        EnemyIndexDocumentDto index = ParseIndex(indexBytes, stamp, diagnostics);

        Dictionary<string, EnemyItemDto> items = new(StringComparer.Ordinal);
        foreach (EnemyIndexEntryDto entry in index.Enemies)
        {
            token.ThrowIfCancellationRequested();
            string id = entry.Id ?? string.Empty;
            if (id.Length == 0)
            {
                continue;
            }

            string file = Path.Combine(bundleRoot, id + ".json");
            if (!File.Exists(file))
            {
                log?.Add("bundle-item-missing", stamp, $"{id}: 随包缺 {file}", "随包 item 文件缺失");
                continue;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(file);
                EnemyItemDto? dto = JsonSerializer.Deserialize<EnemyItemDto>(bytes, Json);
                if (dto is null || !string.Equals(dto.Id, id, StringComparison.Ordinal))
                {
                    log?.Add("bundle-item-parse", stamp, $"{id}: 反序列化为空或 id 不一致", "随包 item 解析失败");
                    continue;
                }

                items[id] = dto;
                if (collectRaw)
                {
                    raw["enemy:" + id] = bytes;
                }
            }
            catch (JsonException ex)
            {
                log?.Add("bundle-item-parse", stamp, $"{id}: {ex.Message}", "随包 item JSON 非法");
            }
        }

        return (new EnemyWebReadResult { Index = index, Items = items, Stamp = stamp, Diagnostics = diagnostics }, raw);
    }

    /// <summary>
    /// 读合并包 <c>bundled-all.json</c>。缺失或损坏返回 <c>null</c>，由调用方回退到逐条扫描。
    /// </summary>
    private EnemyWebReadResult? TryReadMerged(string path, WikiSourceStamp stamp, List<WikiDiagnostic> diagnostics, CancellationToken token)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            MergedBundleDto? merged = JsonSerializer.Deserialize<MergedBundleDto>(bytes, Json);
            if (merged?.Items is not { Count: > 0 } || merged.Index is null)
            {
                log?.Add("bundle-merged-empty", stamp, $"{path} 反序列化为空", "合并包不可用，回退逐条扫描");
                return null;
            }

            token.ThrowIfCancellationRequested();

            // 只保留 id 自洽的条目（与逐条路径同一套校验，避免结构漂移被静默接受）。
            Dictionary<string, EnemyItemDto> items = new(merged.Items.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, EnemyItemDto> kv in merged.Items)
            {
                if (kv.Value is null || !string.Equals(kv.Value.Id, kv.Key, StringComparison.Ordinal))
                {
                    log?.Add("bundle-merged-mismatch", stamp, $"{kv.Key}: 合并包条目 id 不一致", "合并包条目被丢弃");
                    continue;
                }

                items[kv.Key] = kv.Value;
            }

            if (items.Count == 0)
            {
                return null;
            }

            return new EnemyWebReadResult
            {
                Index = merged.Index,
                Items = items,
                Stamp = stamp,
                Diagnostics = diagnostics,
            };
        }
        catch (JsonException ex)
        {
            log?.Add("bundle-merged-parse", stamp, $"{path}: {ex.Message}", "合并包 JSON 非法，回退逐条扫描");
            return null;
        }
        catch (IOException ex)
        {
            log?.Add("bundle-merged-read", stamp, $"{path}: {ex.Message}", "合并包读取失败，回退逐条扫描");
            return null;
        }
    }

    private static EnemyIndexDocumentDto ParseIndex(byte[] bytes, WikiSourceStamp stamp, List<WikiDiagnostic> diagnostics)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(bytes);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return new EnemyIndexDocumentDto
                {
                    Enemies = JsonSerializer.Deserialize<List<EnemyIndexEntryDto>>(doc.RootElement.GetRawText(), Json) ?? [],
                };
            }

            return JsonSerializer.Deserialize<EnemyIndexDocumentDto>(bytes, Json) ?? new();
        }
        catch (JsonException ex)
        {
            diagnostics.Add(new WikiDiagnostic { Kind = "bundle-index-parse", Source = stamp.Source, FallbackReason = stamp.FallbackReason, Detail = ex.Message });
            return new EnemyIndexDocumentDto();
        }
    }

    /// <summary>把随包内容种入 HTTP 缓存键（enemy-index.json / enemy:&lt;id&gt;），使离线基线持久化、缓存统计一致。</summary>
    private async Task SeedCacheAsync(EnemyWebReadResult bundled, Dictionary<string, byte[]> raw, CancellationToken token)
    {
        if (cache is null)
        {
            return;
        }

        try
        {
            foreach (KeyValuePair<string, byte[]> kv in raw)
            {
                cache.Put(kv.Key, kv.Value, null);
            }

            await cache.SaveAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log?.Add("bundle-seed-failed", bundled.Stamp, ex.Message, "随包写入缓存失败（不影响本次展示）");
        }
    }

    /// <summary>合并包 <c>bundled-all.json</c> 的形状（ellen-enemy-bundle-v1）。</summary>
    private sealed class MergedBundleDto
    {
        public EnemyIndexDocumentDto? Index { get; set; }

        public Dictionary<string, EnemyItemDto>? Items { get; set; }
    }
}
