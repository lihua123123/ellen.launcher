// P3/P4 增量缓存：manifest digest、文件级 ETag/hash、图片内容寻址键。
// 纯文件 IO，纯 net10.0；重复启动不下载未变文件，digest 变化只下变文件。
//
// P4 完整性约束：
//  - 落盘一律「临时文件 → 刷新 → 原子 Move」，进程被杀不会留下半个文件被当成有效缓存；
//  - 可校验写入 PutVerified：sha256 不符直接拒绝，不污染缓存；
//  - 索引先写 .tmp 再 Move，并保留 .bak；索引损坏时先回退 .bak，再退化为空索引 + 孤儿清理；
//  - 读时 Touch 记录 LRU，Prune 同时支持「不保留的 key」和「磁盘上限」两种淘汰；
//  - 所有写操作串行（gate），并发读不阻塞。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class WikiCacheEntry
{
    public string Key { get; init; } = string.Empty;
    public string? ETag { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public long Size { get; init; }
    public string CachedAt { get; init; } = string.Empty;

    /// <summary>最近一次读/写时间（UTC），用于 LRU 淘汰。</summary>
    public string LastAccessUtc { get; set; } = string.Empty;
}

public sealed class WikiCacheIndex
{
    public string Schema { get; init; } = "wiki-cache-v3";
    public string? ManifestDigest { get; set; }
    public string? ManifestETag { get; set; }

    /// <summary>P4：按数据集记录上次同步的 digest（如 enemies / avatar / weapon）。</summary>
    public Dictionary<string, string> DatasetDigests { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, WikiCacheEntry> Files { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>缓存统计，供诊断页展示命中率。</summary>
public sealed class WikiCacheStats
{
    public int Hits { get; init; }
    public int Misses { get; init; }
    public int NotModified { get; init; }
    public int Downloads { get; init; }
    public int Failures { get; init; }
    public int BytesDownloaded { get; init; }
    public double HitRate => Hits + Misses == 0 ? 0 : Math.Round(100.0 * Hits / (Hits + Misses), 2);
}

/// <summary>Prune 策略：先删不在 keep 里的 key，再按 LRU 淘汰直到低于磁盘上限。</summary>
public sealed class WikiCachePruneOptions
{
    /// <summary>必须保留的 key（当前数据集仍引用的文件）。</summary>
    public IReadOnlyCollection<string> KeepKeys { get; init; } = [];

    /// <summary>
    /// 按前缀保留：命中前缀的 key 不会被「未引用」规则删除，但仍可被 LRU 按磁盘上限淘汰。
    /// 图片用 <c>image:</c>（按需下载，不参与数据集引用枚举）。
    /// </summary>
    public IReadOnlyCollection<string> KeepPrefixes { get; init; } = [];

    /// <summary>磁盘上限（字节）。0 或负数表示不按大小淘汰。</summary>
    public long MaxBytes { get; init; }

    /// <summary>无论大小都优先保留的 key 前缀（manifest / index 之类的基础设施）。</summary>
    public IReadOnlyCollection<string> ProtectedPrefixes { get; init; } = ["manifest.json", "enemy-index.json"];

    /// <summary>
    /// P5：分层配额。每个层先按自己的上限做 LRU（层内互不挤占），
    /// 再统一受 <see cref="MaxBytes"/> 约束；全局淘汰按 <see cref="WikiCacheTierLimit.EvictionPriority"/> 先后。
    /// 空表示不分层（退回 P4 的单层行为）。
    /// </summary>
    public IReadOnlyList<WikiCacheTierLimit> TierLimits { get; init; } = [];
}

public sealed class WikiCacheTierLimit
{
    public string Name { get; init; } = string.Empty;

    public IReadOnlyCollection<string> Prefixes { get; init; } = [];

    /// <summary>该层磁盘上限（字节）。0 或负数表示该层不单独限制。</summary>
    public long MaxBytes { get; init; }

    /// <summary>受保护层：不参与任何 LRU 淘汰（只受未引用清理影响）。</summary>
    public bool Protected { get; init; }

    /// <summary>全局上限淘汰顺序：数值大的层先淘汰（详情图先于条目数据）。</summary>
    public int EvictionPriority { get; init; }
}

public sealed class WikiCacheTierUsage
{
    public string Name { get; init; } = string.Empty;
    public int Files { get; init; }
    public long Bytes { get; init; }
    public long LimitBytes { get; init; }
    public bool Protected { get; init; }

    public double UsagePercent => LimitBytes <= 0 ? 0 : Math.Round(100.0 * Bytes / LimitBytes, 1);
}

/// <summary>分层归类：与 <see cref="WikiCacheStore"/> 的 key 约定一致，集中在这里避免各调用方各写一套前缀。</summary>
public static class WikiCacheTiers
{
    public const string Infra = "infra";
    public const string Items = "items";
    public const string Images = "images";
    public const string Thumbnails = "thumbs";

    public static string Classify(string key)
    {
        if (key.StartsWith("thumb:", StringComparison.Ordinal))
        {
            return Thumbnails;
        }

        if (key.StartsWith("image:", StringComparison.Ordinal))
        {
            return Images;
        }

        if (key.StartsWith("enemy:", StringComparison.Ordinal))
        {
            return Items;
        }

        return Infra;
    }

    /// <summary>
    /// 1 GiB 总预算的默认分层：infra 64 MiB（保护）/ items 20% / thumbs 18% / images 余量。
    /// 全量原图 1.35 GiB 装不下是已知事实（P5 方案 B：缩略图离线 + 详情在线），
    /// 所以这里不承诺「全量可缓存」，只保证详情图不会把条目数据挤出去。
    /// </summary>
    public static IReadOnlyList<WikiCacheTierLimit> Defaults(long totalBytes)
    {
        long total = totalBytes > 0 ? totalBytes : 1L * 1024 * 1024 * 1024;
        long infra = Math.Min(64L * 1024 * 1024, total / 16);
        long items = Math.Max(16L * 1024 * 1024, total * 20 / 100);
        long thumbs = Math.Max(16L * 1024 * 1024, total * 18 / 100);
        long images = Math.Max(16L * 1024 * 1024, total - infra - items - thumbs);

        return
        [
            new WikiCacheTierLimit { Name = Infra, Prefixes = ["manifest.json", "enemy-index.json"], MaxBytes = infra, Protected = true, EvictionPriority = 0 },
            new WikiCacheTierLimit { Name = Items, Prefixes = ["enemy:"], MaxBytes = items, EvictionPriority = 20 },
            new WikiCacheTierLimit { Name = Images, Prefixes = ["image:"], MaxBytes = images, EvictionPriority = 30 },
            new WikiCacheTierLimit { Name = Thumbnails, Prefixes = ["thumb:"], MaxBytes = thumbs, EvictionPriority = 10 },
        ];
    }

    public static string Classify(string key, IReadOnlyList<WikiCacheTierLimit> limits)
    {
        foreach (WikiCacheTierLimit limit in limits)
        {
            foreach (string prefix in limit.Prefixes)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return limit.Name;
                }
            }
        }

        return Classify(key);
    }
}

public sealed class WikiCachePruneResult
{
    public int RemovedUnreferenced { get; init; }
    public int RemovedByLru { get; init; }
    public int RemovedByTier { get; init; }
    public long BytesBefore { get; init; }
    public long BytesAfter { get; init; }
    public int RemainingFiles { get; init; }
    public IReadOnlyList<WikiCacheTierUsage> Tiers { get; init; } = [];
}

public sealed class WikiCacheStore : IDisposable
{
    private const string TempPrefix = ".tmp-";

    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);
    private WikiCacheIndex index;

    public WikiCacheStore(string root)
    {
        this.root = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Path.GetTempPath(), "EllenWikiCache")
            : root;
        Directory.CreateDirectory(this.root);
        index = LoadIndexWithRecovery();
    }

    public string Root => root;

    public string? ManifestDigest => index.ManifestDigest;

    public string? ManifestETag => index.ManifestETag;

    public int CachedFileCount => index.Files.Count;

    /// <summary>索引加载时是否发生过损坏恢复（供诊断页展示）。</summary>
    public string? RecoveryNote { get; private set; }

    public IEnumerable<string> CachedKeys() => index.Files.Keys.ToList();

    private string IndexPath => Path.Combine(root, "cache-index.json");

    private string BackupPath => IndexPath + ".bak";

    private WikiCacheIndex LoadIndexWithRecovery()
    {
        WikiCacheIndex? primary = TryLoad(IndexPath);
        if (primary is not null)
        {
            return primary;
        }

        WikiCacheIndex? backup = TryLoad(BackupPath);
        if (backup is not null)
        {
            RecoveryNote = "index 损坏，已从 cache-index.json.bak 恢复";
            return backup;
        }

        if (File.Exists(IndexPath) || File.Exists(BackupPath))
        {
            RecoveryNote = "index 与备份都不可读，已重建为空索引（旧文件按孤儿清理）";
        }

        return new WikiCacheIndex();
    }

    private static WikiCacheIndex? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            WikiCacheIndex? parsed = JsonSerializer.Deserialize<WikiCacheIndex>(File.ReadAllText(path));
            return parsed?.Files is null ? null : parsed;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>索引原子落盘：先写 .tmp，保留旧版为 .bak，再 Move 覆盖。</summary>
    public async Task SaveAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            WriteIndexAtomic();
        }
        finally
        {
            gate.Release();
        }
    }

    private void WriteIndexAtomic()
    {
        string temp = IndexPath + TempPrefix + Guid.NewGuid().ToString("N");
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(index, new JsonSerializerOptions { WriteIndented = true });
        using (FileStream stream = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(payload, 0, payload.Length);
            stream.Flush(true);
        }

        if (File.Exists(IndexPath))
        {
            File.Copy(IndexPath, BackupPath, true);
        }

        File.Move(temp, IndexPath, true);
    }

    public void SetManifest(string? digest, string? etag)
    {
        index.ManifestDigest = digest;
        index.ManifestETag = etag;
    }

    /// <summary>记录某数据集上次成功同步的 digest；下一次同步用它判断能否整段跳过。</summary>
    public void SetDatasetDigest(string dataset, string? digest)
    {
        if (string.IsNullOrEmpty(digest))
        {
            index.DatasetDigests.Remove(dataset);
            return;
        }

        index.DatasetDigests[dataset] = digest;
    }

    public string? GetDatasetDigest(string dataset)
        => index.DatasetDigests.TryGetValue(dataset, out string? digest) ? digest : null;

    /// <summary>内容寻址键：id/file 或 id/file:hash（P4 有 assets manifest 后带 hash）。</summary>
    public static string ImageKey(string enemyId, string file, string? hash = null) =>
        string.IsNullOrEmpty(hash) ? $"{enemyId}/{file}" : $"{enemyId}/{file}:{hash}";

    public string FilePath(string cacheKey)
    {
        string safe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey))).ToLowerInvariant();
        return Path.Combine(root, "files", safe[..2], safe);
    }

    public bool TryGet(string cacheKey, out WikiCacheEntry entry)
    {
        if (index.Files.TryGetValue(cacheKey, out entry!))
        {
            entry.LastAccessUtc = DateTime.UtcNow.ToString("O");
            return true;
        }

        return false;
    }

    public byte[]? ReadBytes(string cacheKey)
    {
        string path = FilePath(cacheKey);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (index.Files.TryGetValue(cacheKey, out WikiCacheEntry? entry))
            {
                entry.LastAccessUtc = DateTime.UtcNow.ToString("O");
                // 读时自愈：索引里的 sha 与磁盘不一致 → 判为损坏，交给上层重新下载。
                if (!string.IsNullOrEmpty(entry.Sha256)
                    && !string.Equals(Hash(bytes), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return bytes;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Put(string cacheKey, byte[] bytes, string? etag) => PutCore(cacheKey, bytes, etag, expectedSha256: null);

    /// <summary>
    /// 带校验的写入：<paramref name="expectedSha256"/> 非空时必须与内容哈希一致，否则拒绝写入。
    /// 用于 P4 assets manifest 下载（先验 hash 再进缓存）。
    /// </summary>
    public bool PutVerified(string cacheKey, byte[] bytes, string? etag, string? expectedSha256)
        => PutCore(cacheKey, bytes, etag, expectedSha256);

    private bool PutCore(string cacheKey, byte[] bytes, string? etag, string? expectedSha256)
    {
        string actual = Hash(bytes);
        if (!string.IsNullOrEmpty(expectedSha256)
            && !string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string path = FilePath(cacheKey);
        gate.Wait();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // 内容寻址：已存在且 hash 一致就不重写，避免重复 IO。
            if (!(File.Exists(path) && string.Equals(Hash(File.ReadAllBytes(path)), actual, StringComparison.OrdinalIgnoreCase)))
            {
                string temp = path + TempPrefix + Guid.NewGuid().ToString("N");
                try
                {
                    using (FileStream stream = new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }

                    File.Move(temp, path, true);
                }
                finally
                {
                    TryDelete(temp);
                }
            }

            index.Files[cacheKey] = new WikiCacheEntry
            {
                Key = cacheKey,
                ETag = etag,
                Sha256 = actual,
                Size = bytes.Length,
                CachedAt = DateTime.UtcNow.ToString("O"),
                LastAccessUtc = DateTime.UtcNow.ToString("O"),
            };
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Remove(string cacheKey)
    {
        gate.Wait();
        try
        {
            TryDelete(FilePath(cacheKey));
            index.Files.Remove(cacheKey);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 清理：① 删除不在 <see cref="WikiCachePruneOptions.KeepKeys"/> 里的条目；
    /// ② 分层配额：每层按自己的上限做 LRU（层内淘汰，不互相挤占）；
    /// ③ 全局磁盘上限：按层的淘汰优先级（详情图先于条目）再做一次 LRU。
    /// </summary>
    public WikiCachePruneResult Prune(WikiCachePruneOptions options)
    {
        gate.Wait();
        try
        {
            HashSet<string> keep = new(options.KeepKeys, StringComparer.Ordinal);
            long bytesBefore = index.Files.Values.Sum(v => v.Size);

            List<string> unreferenced = [.. index.Files.Keys
                .Where(k => !keep.Contains(k)
                    && !IsProtected(k, options.KeepPrefixes)
                    && !IsProtected(k, options.ProtectedPrefixes))];
            foreach (string key in unreferenced)
            {
                TryDelete(FilePath(key));
                index.Files.Remove(key);
            }

            int removedByTier = 0;
            foreach (WikiCacheTierLimit limit in options.TierLimits)
            {
                if (limit.Protected || limit.MaxBytes <= 0)
                {
                    continue;
                }

                List<WikiCacheEntry> tierEntries = [.. index.Files.Values
                    .Where(e => string.Equals(WikiCacheTiers.Classify(e.Key, options.TierLimits), limit.Name, StringComparison.Ordinal))];
                long tierTotal = tierEntries.Sum(e => e.Size);
                if (tierTotal <= limit.MaxBytes)
                {
                    continue;
                }

                foreach (WikiCacheEntry entry in tierEntries
                    .OrderBy(e => ParseAccess(e.LastAccessUtc))
                    .ThenBy(e => e.Size))
                {
                    if (tierTotal <= limit.MaxBytes)
                    {
                        break;
                    }

                    TryDelete(FilePath(entry.Key));
                    index.Files.Remove(entry.Key);
                    tierTotal -= entry.Size;
                    removedByTier++;
                }
            }

            int removedByLru = 0;
            if (options.MaxBytes > 0)
            {
                long total = index.Files.Values.Sum(v => v.Size);
                if (total > options.MaxBytes)
                {
                    // 先淘汰优先级高的层（images=30 → items=20 → thumbs=10），同层内最久未访问的先删。
                    foreach (WikiCacheEntry entry in index.Files.Values
                        .Where(e => !IsProtected(e.Key, options.ProtectedPrefixes))
                        .OrderByDescending(e => TierPriority(e.Key, options.TierLimits))
                        .ThenBy(e => ParseAccess(e.LastAccessUtc))
                        .ThenBy(e => e.Size))
                    {
                        if (total <= options.MaxBytes)
                        {
                            break;
                        }

                        TryDelete(FilePath(entry.Key));
                        index.Files.Remove(entry.Key);
                        total -= entry.Size;
                        removedByLru++;
                    }
                }
            }

            return new WikiCachePruneResult
            {
                RemovedUnreferenced = unreferenced.Count,
                RemovedByLru = removedByLru,
                RemovedByTier = removedByTier,
                BytesBefore = bytesBefore,
                BytesAfter = index.Files.Values.Sum(v => v.Size),
                RemainingFiles = index.Files.Count,
                Tiers = GetTierUsageCore(options.TierLimits),
            };
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>分层用量（诊断页展示）——不改索引，只统计当前磁盘占用。</summary>
    public IReadOnlyList<WikiCacheTierUsage> GetTierUsage(IReadOnlyList<WikiCacheTierLimit>? limits = null)
    {
        gate.Wait();
        try
        {
            return GetTierUsageCore(limits ?? []);
        }
        finally
        {
            gate.Release();
        }
    }

    private List<WikiCacheTierUsage> GetTierUsageCore(IReadOnlyList<WikiCacheTierLimit> limits)
    {
        if (limits.Count == 0)
        {
            return [];
        }

        List<WikiCacheTierUsage> usage = [];
        foreach (WikiCacheTierLimit limit in limits)
        {
            List<WikiCacheEntry> entries = [.. index.Files.Values
                .Where(e => string.Equals(WikiCacheTiers.Classify(e.Key, limits), limit.Name, StringComparison.Ordinal))];
            usage.Add(new WikiCacheTierUsage
            {
                Name = limit.Name,
                Files = entries.Count,
                Bytes = entries.Sum(e => e.Size),
                LimitBytes = limit.MaxBytes,
                Protected = limit.Protected,
            });
        }

        return usage;
    }

    private static int TierPriority(string key, IReadOnlyList<WikiCacheTierLimit> limits)
    {
        string tier = WikiCacheTiers.Classify(key, limits);
        foreach (WikiCacheTierLimit limit in limits)
        {
            if (string.Equals(limit.Name, tier, StringComparison.Ordinal))
            {
                return limit.EvictionPriority;
            }
        }

        return 0;
    }

    /// <summary>兼容 P3 的旧调用：只按 keep 清理，不设磁盘上限。</summary>
    public WikiCachePruneResult Prune(IEnumerable<string> keepKeys)
        => Prune(new WikiCachePruneOptions { KeepKeys = [.. keepKeys] });

    private static bool IsProtected(string key, IReadOnlyCollection<string> prefixes)
        => prefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));

    private static DateTime ParseAccess(string? value)
        => DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
            ? parsed
            : DateTime.MinValue;

    /// <summary>
    /// 全量自检：文件缺失或 sha 不符的条目从索引移除并返回明细。
    /// 用于 P4 全量图片校验与启动期可选巡检（不要放首屏同步路径）。
    /// </summary>
    public WikiCacheVerifyResult VerifyAll()
    {
        gate.Wait();
        try
        {
            List<WikiCacheVerifyFailure> failures = [];
            int checkedCount = index.Files.Count;
            long bytes = 0;
            foreach (WikiCacheEntry entry in index.Files.Values.ToList())
            {
                string path = FilePath(entry.Key);
                if (!File.Exists(path))
                {
                    failures.Add(new WikiCacheVerifyFailure { Key = entry.Key, Reason = "文件缺失" });
                    index.Files.Remove(entry.Key);
                    continue;
                }

                byte[] bytesOnDisk = File.ReadAllBytes(path);
                if (!string.IsNullOrEmpty(entry.Sha256)
                    && !string.Equals(Hash(bytesOnDisk), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add(new WikiCacheVerifyFailure { Key = entry.Key, Reason = "sha256 不符" });
                    index.Files.Remove(entry.Key);
                    TryDelete(path);
                    continue;
                }

                bytes += entry.Size;
            }

            return new WikiCacheVerifyResult
            {
                Checked = checkedCount,
                Failures = failures,
                Bytes = bytes,
            };
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>清理上次进程被中断留下的临时文件（启动时调一次即可）。</summary>
    public int SweepTempFiles()
    {
        int removed = 0;
        string filesRoot = Path.Combine(root, "files");
        if (!Directory.Exists(filesRoot))
        {
            return 0;
        }

        foreach (string dir in Directory.EnumerateDirectories(filesRoot))
        {
            foreach (string file in Directory.EnumerateFiles(dir, TempPrefix + "*"))
            {
                if (TryDelete(file))
                {
                    removed++;
                }
            }
        }

        foreach (string file in Directory.EnumerateFiles(root, "cache-index.json" + TempPrefix + "*"))
        {
            if (TryDelete(file))
            {
                removed++;
            }
        }

        return removed;
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public void Dispose() => gate.Dispose();
}

public sealed class WikiCacheVerifyFailure
{
    public string Key { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class WikiCacheVerifyResult
{
    public int Checked { get; init; }
    public long Bytes { get; init; }
    public IReadOnlyList<WikiCacheVerifyFailure> Failures { get; init; } = [];
    public bool Pass => Failures.Count == 0;
}
