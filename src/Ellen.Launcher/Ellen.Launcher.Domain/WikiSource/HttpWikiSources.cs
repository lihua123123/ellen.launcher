// P3 HTTP 运行时源：ILauncherSnapshotSource / IEnemyWebSource 的 HTTP 实现。
// 只读公开 API 的 JSON；文件级 ETag + 内容 hash 增量缓存；失败显式诊断，不静默回退。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

/// <summary>C1：manifest 同步。digest 未变且 ETag 304 时不下载；digest 变化只下变文件。</summary>
public sealed class HttpLauncherSnapshotSource : ILauncherSnapshotSource, ICachedLauncherSnapshotSource, IWikiSourceStatsProvider, IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly WikiHttpClient http;
    private readonly WikiCacheStore cache;
    private readonly WikiDiagnosticsLog log;
    private readonly HttpWikiSourceOptions options;

    public HttpLauncherSnapshotSource(HttpWikiSourceOptions options, WikiCacheStore cache, WikiDiagnosticsLog? log = null)
    {
        this.options = options;
        this.cache = cache;
        this.log = log ?? new WikiDiagnosticsLog();
        http = new WikiHttpClient(options, this.log, WikiSourceStamp.FromEllenWiki("C1 HTTP", null, null));
    }

    public WikiCacheStats Stats => http.Stats;

    public async ValueTask<LauncherSnapshotReadResult> ReadAsync(CancellationToken token = default)
    {
        string key = "manifest.json";
        cache.TryGet(key, out WikiCacheEntry? previous);
        WikiHttpResponse response = await http.GetAsync(options.ManifestPath, previous?.ETag, token).ConfigureAwait(false);

        byte[]? bytes = response.Bytes;
        if (response.NotModified && cache.ReadBytes(key) is { } cachedBytes)
        {
            http.RecordHit();
            bytes = cachedBytes;
        }
        else if (response.Success && bytes is not null)
        {
            http.RecordMiss();
            cache.Put(key, bytes, response.ETag);
        }
        else
        {
            // 网络失败：优先用上一版缓存，显式记录 fallback，不静默。
            bytes = cache.ReadBytes(key);
            log.Add("snapshot-fallback", WikiSourceStamp.FromBundled("HTTP 失败，使用上一版缓存"),
                $"manifest 同步失败：{response.Error}；使用缓存 {bytes is not null}", response.Error);
        }

        LauncherManifestDto manifest = new();
        if (bytes is not null)
        {
            try { manifest = JsonSerializer.Deserialize<LauncherManifestDto>(bytes, Json) ?? new(); }
            catch (JsonException ex) { log.Add("manifest-parse", WikiSourceStamp.FromBundled("manifest 解析失败"), ex.Message, ex.Message); }
        }

        string? previousDigest = cache.ManifestDigest;
        bool digestChanged = !string.IsNullOrEmpty(previousDigest) && !string.Equals(previousDigest, manifest.Digest, StringComparison.Ordinal);
        cache.SetManifest(manifest.Digest, response.ETag);
        await cache.SaveAsync(token).ConfigureAwait(false);

        WikiSourceStamp stamp = WikiSourceStamp.FromEllenWiki("C1 /api/launcher HTTP", manifest.Digest, manifest.GeneratedAt);
        LauncherSnapshotReadResult result = new()
        {
            Manifest = manifest,
            Files = manifest.Files.Select(f => f.Path).ToList(),
            Stamp = stamp,
            Diagnostics = log.Records,
            Curves = CurveContract.Validate(manifest),
        };

        if (digestChanged)
        {
            log.Add("digest-changed", stamp, $"manifest digest {previousDigest} → {manifest.Digest}；按文件 ETag 只下变文件", null);
        }

        if (result.Curves.Alert is { } alert)
        {
            log.Add("curve-strict-alert", stamp, alert, CurveContract.ValidateMissingReason);
        }

        return result;
    }

    /// <summary>纯本地缓存读取 manifest（不触网），首屏用它取 digest/generatedAt。</summary>
    public ValueTask<LauncherSnapshotReadResult> ReadCachedAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        LauncherManifestDto manifest = new();
        if (cache.ReadBytes("manifest.json") is { } bytes)
        {
            try { manifest = JsonSerializer.Deserialize<LauncherManifestDto>(bytes, Json) ?? new(); }
            catch (JsonException ex) { log.Add("manifest-parse", WikiSourceStamp.FromBundled("缓存 manifest 解析失败"), ex.Message, ex.Message); }
        }

        return ValueTask.FromResult(new LauncherSnapshotReadResult
        {
            Manifest = manifest,
            Files = manifest.Files.Select(f => f.Path).ToList(),
            Stamp = WikiSourceStamp.FromEllenWiki("C1 cache", manifest.Digest ?? cache.ManifestDigest, manifest.GeneratedAt),
            Diagnostics = log.Records,
            Curves = CurveContract.Validate(manifest),
        });
    }

    public void Dispose() => http.Dispose();
}

/// <summary>C2：index + 753 items，文件级 ETag 缓存；只下变文件；DTO 级解析，失败逐条记录。</summary>
public sealed class HttpEnemyWebSource : IEnemyWebSource, ICachedEnemyWebSource, IWikiSourceStatsProvider, IDisposable
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private readonly WikiHttpClient http;
    private readonly WikiCacheStore cache;
    private readonly WikiDiagnosticsLog log;
    private readonly HttpWikiSourceOptions options;
    private readonly HttpLauncherSnapshotSource? snapshot;
    private readonly WikiSourceStamp stamp = WikiSourceStamp.FromEllenWiki("C2 /api/enemies HTTP", null, null);
    private bool tempSwept;
    private bool datasetsMissingLogged;

    private static WikiDatasetDto? TryGetDataset(LauncherSnapshotReadResult? manifest, string dataset)
    {
        if (manifest?.Manifest.Datasets is not { Count: > 0 } datasets
            || !datasets.TryGetValue(dataset, out WikiDatasetDto? dto))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(dto.Digest) ? null : dto;
    }

    /// <param name="snapshot">
    /// 可选：传入后每次同步会先读一次 C1 manifest（带 ETag，通常 304），
    /// 把 manifest 的 digest/generatedAt 带进诊断页 —— 敌人 API 本身不暴露这两项。
    /// </param>
    public HttpEnemyWebSource(
        HttpWikiSourceOptions options,
        WikiCacheStore cache,
        WikiDiagnosticsLog? log = null,
        HttpLauncherSnapshotSource? snapshot = null)
    {
        this.options = options;
        this.cache = cache;
        this.log = log ?? new WikiDiagnosticsLog();
        this.snapshot = snapshot;
        http = new WikiHttpClient(options, this.log, stamp);
    }

    public WikiCacheStats Stats => http.Stats;

    public async ValueTask<EnemyWebReadResult> ReadAsync(CancellationToken token = default)
    {
        if (!tempSwept)
        {
            // 上次进程被中断可能留下 .tmp-*；启动期清一次，避免被当成有效缓存。
            tempSwept = true;
            int swept = cache.SweepTempFiles();
            if (swept > 0)
            {
                log.Add("cache-temp-swept", WikiSourceStamp.FromBundled("启动清理临时文件"), $"清理 {swept} 个中断残留的临时文件", null);
            }
        }

        WikiSourceStamp effectiveStamp = stamp;
        LauncherSnapshotReadResult? manifestResult = null;
        if (snapshot is not null)
        {
            manifestResult = await snapshot.ReadAsync(token).ConfigureAwait(false);
            effectiveStamp = WikiSourceStamp.FromEllenWiki("C2 /api/enemies HTTP", manifestResult.Stamp.Digest, manifestResult.Stamp.GeneratedAt);
        }

        // P4-3：manifest.datasets 里 enemies 的 digest 未变且缓存完整 → 整段跳过 C2 网络请求。
        WikiDatasetDto? enemiesDataset = TryGetDataset(manifestResult, "enemies");
        string? enemiesDigest = enemiesDataset?.Digest;
        if (enemiesDigest is { Length: > 0 }
            && string.Equals(cache.GetDatasetDigest("enemies"), enemiesDigest, StringComparison.Ordinal))
        {
            EnemyWebReadResult cached = await ReadCachedAsync(token).ConfigureAwait(false);
            int expected = enemiesDataset is { Count: > 0 } ? enemiesDataset.Count : EnemyItemSchema.ExpectedItemCount;
            if (cached.Items.Count == expected)
            {
                log.Add("dataset-cache-hit", effectiveStamp,
                    $"datasets.enemies digest 未变（{enemiesDigest[..Math.Min(8, enemiesDigest.Length)]}…），跳过 C2 网络请求",
                    null);
                return new EnemyWebReadResult
                {
                    Index = cached.Index,
                    Items = cached.Items,
                    Stamp = effectiveStamp,
                    Diagnostics = log.Records,
                    DatasetCacheHit = true,
                };
            }
        }

        if (manifestResult is not null && string.IsNullOrEmpty(enemiesDigest) && !datasetsMissingLogged)
        {
            datasetsMissingLogged = true;
            log.Add("datasets-missing", effectiveStamp,
                "manifest 未提供 datasets.enemies digest，退化为文件级 ETag 增量（仍只下变文件）",
                "分数据集 digest 未提供");
        }

        EnemyIndexDocumentDto index = await ReadIndexAsync(token).ConfigureAwait(false);
        Dictionary<string, EnemyItemDto> items = new(StringComparer.Ordinal);
        List<string> ids = [.. index.Enemies.Select(e => e.Id)
            .Where(id => !string.IsNullOrEmpty(id)).OrderBy(id => id, StringComparer.Ordinal)];

        // index 为空（网络失败且无缓存）时退回缓存里已有的 enemy key，保证离线降级不空屏。
        if (ids.Count == 0)
        {
            ids = [.. cache.CachedKeys().Where(k => k.StartsWith("enemy:", StringComparison.Ordinal)).Select(k => k[6..])];
        }

        // P5：条目并发下载（默认 4）。真实网络下顺序请求会被 RTT 放大到几分钟；
        // 并发只影响起始节奏（WikiHttpClient 仍有 20ms 全局限速），不改变逐条缓存与校验语义。
        object itemsGate = new();
        using SemaphoreSlim itemLimit = new(Math.Max(1, options.ItemConcurrency), Math.Max(1, options.ItemConcurrency));
        List<Task> itemTasks = [];
        foreach (string id in ids)
        {
            await itemLimit.WaitAsync(token).ConfigureAwait(false);
            itemTasks.Add(Task.Run(async () =>
            {
                try
                {
                    EnemyItemDto? dto = await ReadItemAsync(id, token).ConfigureAwait(false);
                    if (dto is not null)
                    {
                        lock (itemsGate)
                        {
                            items[id] = dto;
                        }
                    }
                }
                finally
                {
                    itemLimit.Release();
                }
            }, token));
        }

        await Task.WhenAll(itemTasks).ConfigureAwait(false);

        // P4 缓存治理：只清理不再引用的 enemy:*，图片按前缀保留（仍可被 LRU 按磁盘上限淘汰），
        // manifest/index 属基础设施必留。超过上限时按最久未访问淘汰。
        WikiCachePruneResult pruned = cache.Prune(new WikiCachePruneOptions
        {
            KeepKeys = [.. items.Keys.Select(k => "enemy:" + k).Append("manifest.json").Append("enemy-index.json")],
            KeepPrefixes = ["image:", "thumb:"],
            MaxBytes = options.CacheMaxBytes,
            TierLimits = options.EffectiveTierLimits,
        });
        if (pruned.RemovedByTier > 0)
        {
            log.Add("cache-tier-pruned", effectiveStamp,
                $"分层配额触发淘汰 {pruned.RemovedByTier} 个文件（{TierText(pruned.Tiers)}）",
                "缓存分层已限流，非数据降级");
        }

        if (pruned.RemovedByLru > 0)
        {
            log.Add("cache-pruned", effectiveStamp,
                $"磁盘上限 {options.CacheMaxBytes} 字节触发 LRU 淘汰 {pruned.RemovedByLru} 个文件（{pruned.BytesBefore} → {pruned.BytesAfter}；{TierText(pruned.Tiers)}）",
                "缓存超限已淘汰，非数据降级");
        }

        // 记录本次数据集 digest，供下次整段跳过判定（上游未提供时保持空）。
        cache.SetDatasetDigest("enemies", enemiesDigest);
        await cache.SaveAsync(token).ConfigureAwait(false);

        if (items.Count != EnemyItemSchema.ExpectedItemCount)
        {
            log.Add("enemy-count-mismatch", effectiveStamp,
                $"DTO 级解析 {items.Count}/{EnemyItemSchema.ExpectedItemCount}；文件级统计不能当契约验收",
                "敌人条目不足 753");
        }

        return new EnemyWebReadResult { Index = index, Items = items, Stamp = effectiveStamp, Diagnostics = log.Records };
    }

    /// <summary>
    /// 纯本地缓存读取：不发任何 HTTP 请求。首屏用它保证启动不被同步阻塞。
    /// 缓存为空时返回 0 条（上层显示「后台同步中」并保持 legacy）。
    /// </summary>
    public ValueTask<EnemyWebReadResult> ReadCachedAsync(CancellationToken token = default)
    {
        EnemyIndexDocumentDto index = ParseIndexBytes(cache.ReadBytes("enemy-index.json"));
        Dictionary<string, EnemyItemDto> items = new(StringComparer.Ordinal);
        foreach (EnemyIndexEntryDto entry in index.Enemies)
        {
            token.ThrowIfCancellationRequested();
            string id = entry.Id ?? string.Empty;
            if (id.Length == 0)
            {
                continue;
            }

            byte[]? bytes = cache.ReadBytes("enemy:" + id);
            if (bytes is null)
            {
                continue;
            }

            EnemyItemDto? dto = TryDeserializeItem(bytes, id, log);
            if (dto is not null)
            {
                items[id] = dto;
            }
        }

        WikiSourceStamp cachedStamp = WikiSourceStamp.FromEllenWiki("C2 cache", cache.ManifestDigest, null);
        return ValueTask.FromResult(new EnemyWebReadResult
        {
            Index = index,
            Items = items,
            Stamp = cachedStamp,
            Diagnostics = log.Records,
        });
    }

    private static string TierText(IReadOnlyList<WikiCacheTierUsage> tiers)
    {
        if (tiers.Count == 0)
        {
            return "未分层";
        }

        return string.Join("/", tiers.Select(t => $"{t.Name} {t.Bytes / 1024 / 1024}MiB{(t.LimitBytes > 0 ? $"/{t.LimitBytes / 1024 / 1024}MiB" : string.Empty)}"));
    }

    private static EnemyIndexDocumentDto ParseIndexBytes(byte[]? bytes)    {
        if (bytes is null)
        {
            return new EnemyIndexDocumentDto();
        }

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
        catch (JsonException)
        {
            return new EnemyIndexDocumentDto();
        }
    }

    private static EnemyItemDto? TryDeserializeItem(byte[] bytes, string id, WikiDiagnosticsLog log)
    {
        try
        {
            EnemyItemDto? dto = JsonSerializer.Deserialize<EnemyItemDto>(bytes, Json);
            if (dto is null || !string.Equals(dto.Id, id, StringComparison.Ordinal))
            {
                log.Add("item-parse", WikiSourceStamp.FromBundled("缓存 item 解析失败"), $"{id}: 反序列化为空或 id 不一致", "DTO 解析失败");
                return null;
            }

            return dto;
        }
        catch (JsonException ex)
        {
            log.Add("item-parse", WikiSourceStamp.FromBundled("缓存 item 解析失败"), $"{id}: {ex.Message}", "DTO 解析失败（检查 union 类型登记）");
            return null;
        }
    }

    private async Task<EnemyIndexDocumentDto> ReadIndexAsync(CancellationToken token)
    {
        const string key = "enemy-index.json";
        cache.TryGet(key, out WikiCacheEntry? previous);
        WikiHttpResponse response = await http.GetAsync(options.EnemyIndexPath, previous?.ETag, token).ConfigureAwait(false);
        byte[]? bytes = response.NotModified ? cache.ReadBytes(key) : response.Bytes;
        if (bytes is null && cache.ReadBytes(key) is { } fallback)
        {
            bytes = fallback;
            log.Add("index-fallback", WikiSourceStamp.FromBundled("HTTP 失败，用缓存 index"), response.Error ?? "无响应", response.Error);
        }

        if (bytes is not null)
        {
            if (response.Success && !response.NotModified) { http.RecordMiss(); cache.Put(key, bytes, response.ETag); }
            else http.RecordHit();

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
                log.Add("index-parse", stamp, ex.Message, "index JSON 解析失败");
            }
        }

        return new EnemyIndexDocumentDto();
    }

    private async Task<EnemyItemDto?> ReadItemAsync(string id, CancellationToken token)
    {
        string key = "enemy:" + id;
        cache.TryGet(key, out WikiCacheEntry? previous);
        WikiHttpResponse response = await http.GetAsync(options.EnemyItemPath(id), previous?.ETag, token).ConfigureAwait(false);
        byte[]? bytes;
        if (response.NotModified)
        {
            bytes = cache.ReadBytes(key);
            http.RecordHit();
        }
        else if (response.Success && response.Bytes is not null)
        {
            bytes = response.Bytes;
            http.RecordMiss();
            cache.Put(key, bytes, response.ETag);
        }
        else
        {
            bytes = cache.ReadBytes(key);
            log.Add("item-fallback", WikiSourceStamp.FromBundled("HTTP 失败，用缓存 item"),
                $"{id}: {response.Error}；缓存 {(bytes is not null ? "命中" : "缺失")}", response.Error);
        }

        if (bytes is null)
        {
            return null;
        }

        try
        {
            EnemyItemDto? dto = JsonSerializer.Deserialize<EnemyItemDto>(bytes, Json);
            if (dto is null || !string.Equals(dto.Id, id, StringComparison.Ordinal))
            {
                log.Add("item-parse", stamp, $"{id}: 反序列化为空或 id 不一致", "DTO 解析失败");
                return null;
            }

            return dto;
        }
        catch (JsonException ex)
        {
            log.Add("item-parse", stamp, $"{id}: {ex.Message}", "DTO 解析失败（检查 union 类型登记）");
            return null;
        }
    }

    /// <summary>
    /// 按需下载单张敌人图片到内容寻址缓存（键 <c>image:&lt;id&gt;/&lt;file&gt;</c>，有 hash 时带 hash）。
    /// P3/P4 语义：图片仍是在线 + 缓存，缓存只覆盖访问过的图；离线冷启动会失败并记录。
    /// </summary>
    /// <param name="expectedSha256">assets manifest 提供的 sha256；非空时先验后写，不符则拒收。</param>
    public async ValueTask<bool> TryCacheImageAsync(string id, string file, string? expectedSha256 = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(file))
        {
            return false;
        }

        string key = "image:" + WikiCacheStore.ImageKey(id, file, expectedSha256);
        if (cache.TryGet(key, out _) && cache.ReadBytes(key) is not null)
        {
            http.RecordHit();
            return true;
        }

        WikiHttpResponse response = await http.GetAsync(options.EnemyImagePath(id, file), null, token).ConfigureAwait(false);
        if (response.Success && response.Bytes is { Length: > 0 })
        {
            if (!cache.PutVerified(key, response.Bytes, response.ETag, expectedSha256))
            {
                log.Add("image-hash-mismatch", WikiSourceStamp.FromBundled("图片 hash 校验失败"),
                    $"{id}/{file}: 期望 {expectedSha256}，实际 {WikiCacheStore.Hash(response.Bytes)}", "图片内容与 assets manifest 不符，已拒收");
                return false;
            }

            http.RecordMiss();
            return true;
        }

        // 失败计数由 WikiHttpClient.GetAsync 内部累加，这里不重复计。
        log.Add("image-download-failure", WikiSourceStamp.FromBundled("图片下载失败"),
            $"{id}/{file}: {response.Error ?? "空响应"}", "图片按需缓存失败，UI 降级在线 URL/占位图");
        return false;
    }

    /// <summary>
    /// P4：读取上游 assets manifest。缺失/404 时返回 null 并显式记录 —— 此时图片只能在线 + 按需缓存，
    /// **不得对外宣称离线可用**。
    /// </summary>
    public async ValueTask<WikiAssetManifest?> ReadAssetsManifestAsync(CancellationToken token = default)
    {
        WikiHttpResponse response = await http.GetAsync(options.EnemyAssetsManifestPath, null, token).ConfigureAwait(false);
        if (response.Success && response.Bytes is { Length: > 0 })
        {
            WikiAssetManifest? manifest = WikiAssetManifest.Parse(response.Bytes);
            if (manifest is null)
            {
                log.Add("assets-manifest-invalid", stamp, "assets manifest 解析失败或 schema 不符", "图片保持在线+按需缓存");
            }

            return manifest;
        }

        log.Add("assets-manifest-missing", stamp,
            $"{options.EnemyAssetsManifestPath}: {response.Error ?? "无响应"}；图片保持在线 + 按需缓存，不宣称离线",
            "上游未提供 assets manifest");
        return null;
    }

    /// <summary>
    /// P4：按 assets manifest 全量/限量同步图片。可恢复：已缓存且 sha 一致直接跳过；
    /// sha 不符先删旧再重下；下载后先验 hash 再写。返回真实统计，不做「已同步」口头声明。
    /// </summary>
    public async Task<WikiImageSyncResult> SyncImagesAsync(
        WikiAssetManifest manifest,
        int maxFiles = int.MaxValue,
        int concurrency = 4,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
        int alreadyCached = 0;
        int downloaded = 0;
        int failed = 0;
        int hashRejected = 0;
        long bytes = 0;
        int total = 0;

        using SemaphoreSlim limit = new(Math.Max(1, concurrency), Math.Max(1, concurrency));
        List<Task> tasks = [];
        foreach (EnemyAssetFileDto file in manifest.Dto.Files.Take(maxFiles))
        {
            token.ThrowIfCancellationRequested();
            total++;
            await limit.WaitAsync(token).ConfigureAwait(false);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    (bool ok, bool cached, long size, bool rejected) = await SyncOneImageAsync(file, token).ConfigureAwait(false);
                    if (cached) { Interlocked.Increment(ref alreadyCached); }
                    else if (ok) { Interlocked.Increment(ref downloaded); Interlocked.Add(ref bytes, size); }
                    else if (rejected) { Interlocked.Increment(ref hashRejected); }
                    else { Interlocked.Increment(ref failed); }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    log.Add("image-sync-failure", stamp, $"{file.Key}: {ex.Message}", "图片同步异常");
                }
                finally
                {
                    limit.Release();
                }
            }, token));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        watch.Stop();
        await cache.SaveAsync(token).ConfigureAwait(false);

        return new WikiImageSyncResult
        {
            Total = total,
            AlreadyCached = alreadyCached,
            Downloaded = downloaded,
            Failed = failed,
            HashRejected = hashRejected,
            Bytes = bytes,
            ElapsedMs = Math.Round(watch.Elapsed.TotalMilliseconds, 3),
        };
    }

    private async Task<(bool Ok, bool Cached, long Size, bool Rejected)> SyncOneImageAsync(EnemyAssetFileDto file, CancellationToken token)
    {
        string key = "image:" + file.Key;
        if (cache.TryGet(key, out WikiCacheEntry entry)
            && string.Equals(entry.Sha256, file.Sha256, StringComparison.OrdinalIgnoreCase)
            && cache.ReadBytes(key) is not null)
        {
            return (true, true, entry.Size, false);
        }

        WikiHttpResponse response = await http.GetAsync(options.EnemyImagePath(file.Id, file.File), null, token).ConfigureAwait(false);
        if (response.Success && response.Bytes is { Length: > 0 })
        {
            if (!cache.PutVerified(key, response.Bytes, response.ETag, file.Sha256))
            {
                log.Add("image-hash-mismatch", WikiSourceStamp.FromBundled("图片 hash 校验失败"),
                    $"{file.Key}: 期望 {file.Sha256}，实际 {WikiCacheStore.Hash(response.Bytes)}", "已拒收，不写入缓存");
                return (false, false, 0, true);
            }

            http.RecordMiss();
            return (true, false, response.Bytes.Length, false);
        }

        log.Add("image-download-failure", WikiSourceStamp.FromBundled("图片下载失败"),
            $"{file.Key}: {response.Error ?? "空响应"}", "离线包同步失败，该图仍走在线 URL/占位图");
        return (false, false, 0, false);
    }

    public void Dispose() => http.Dispose();
}

/// <summary>
/// 本地镜像 handler：把 /api/** 映射到 ellen-wiki/dist/api/**，用于单测与离线演练。
/// 不联网、可真跑 753 条；生产用真实 HttpClientHandler。
/// </summary>
public sealed class LocalMirrorHandler : HttpMessageHandler
{
    private readonly string distRoot;

    public LocalMirrorHandler(string distRoot)
    {
        this.distRoot = distRoot;
    }

    private int requestCount;
    public int RequestCount => requestCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref requestCount);
        // Uri.EscapeDataString 会把中文 slug 编码成 %XX；服务器按解码后的路径找文件。
        // 用 LocalPath（已 percent-decode）而非 AbsolutePath，否则 224 个中文 id 会 404。
        string relative = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath).TrimStart('/');
        string file = Path.Combine(distRoot, relative.Replace('/', Path.DirectorySeparatorChar));

        // 条件请求：本地镜像按文件大小+修改时间生成弱 ETag，支持 304 演练。
        if (!File.Exists(file))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{\"error\":\"not found\"}") });
        }

        byte[] bytes = File.ReadAllBytes(file);
        FileInfo info = new(file);
        string etag = $"\"{bytes.Length:x}-{info.LastWriteTimeUtc.Ticks:x}\"";
        if (request.Headers.IfNoneMatch.Any(e => string.Equals(e.Tag, etag, StringComparison.Ordinal)))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        }

        HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        return Task.FromResult(response);
    }
}
