// P3 演练器：真实测量冷/热启动、首次/增量同步、图片按需缓存命中与失败率，
// 并给出诊断页所需的 source/digest/generatedAt/fallbackReason/命中率。
// 全部通过可注入的 HttpMessageHandler（本地 dist 镜像）真实跑，不联网也能验证增量逻辑。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class WikiP3Startup
{
    public double ColdStartLegacyFirstScreenMs { get; init; }
    public double ColdStartWebFirstScreenMs { get; init; }

    /// <summary>热启动首屏：只读 manifest + index（列表页真实需要的数据量）。</summary>
    public double HotStartWebFirstScreenMs { get; init; }

    /// <summary>详情页全量预热：把 753 条 item 从本地缓存读一遍的耗时，单独计量不与首屏混算。</summary>
    public double HotStartFullItemsMs { get; init; }

    public int HotStartFullItemsRead { get; init; }
    public bool StartupBlockedBySync { get; init; }
    public string ColdWebFallbackReason { get; init; } = string.Empty;
    public int HotStartCachedItems { get; init; }
}

public sealed class WikiP3Sync
{
    public double FirstSyncMs { get; init; }
    public double IncrementalSyncMs { get; init; }
    public int FirstSyncDownloads { get; init; }
    public int FirstSyncRequests { get; init; }
    public int IncrementalDownloads { get; init; }
    public int IncrementalNotModified { get; init; }
    public int IncrementalRequests { get; init; }
    public int ItemsIncremental { get; init; }
}

public sealed class WikiP3Image
{
    public int Sampled { get; init; }
    public int ColdHits { get; init; }
    public int ColdMisses { get; init; }
    public int ColdFailures { get; init; }
    public int WarmHits { get; init; }
    public double ColdHitRate { get; init; }
    public double WarmHitRate { get; init; }
    public double FailureRate { get; init; }
    public double ColdDownloadMs { get; init; }
    public double WarmResolveMs { get; init; }
}

public sealed class WikiP3Flag
{
    public string DefaultSource { get; init; } = string.Empty;
    public bool DefaultIsLegacy { get; init; }
    public bool SwitchingPersisted { get; init; }
    public bool RestartKeepsWeb { get; init; }
    public bool RollbackPersisted { get; init; }
    public bool DirtyValueFallsBackToLegacy { get; init; }
}

public sealed class WikiP3DiagnosticsPage
{
    public string Source { get; init; } = string.Empty;
    public string? Digest { get; init; }
    public string? GeneratedAt { get; init; }
    public string? FallbackReason { get; init; }
    public int ItemCount { get; init; }

    /// <summary>源状态：源正常 / 源降级（与字段告警分开）。</summary>
    public string SourceStateText { get; init; } = string.Empty;

    /// <summary>字段级告警：曲线不在 zip、缓存淘汰等；源本身可用。</summary>
    public string FieldWarningText { get; init; } = string.Empty;

    /// <summary>C1 manifest 统计（与 C2 分开，避免 754/755 混淆）。</summary>
    public WikiCacheStats C1Cache { get; init; } = new();

    /// <summary>C2 index + 753 items + 图片统计。</summary>
    public WikiCacheStats C2Cache { get; init; } = new();

    public string C1CacheText { get; init; } = string.Empty;
    public string C2CacheText { get; init; } = string.Empty;
    public double CacheHitRate { get; init; }
    public int CacheNotModified { get; init; }
    public int CacheDownloads { get; init; }
    public double ImageFailureRate { get; init; }
    public int DiagnosticsCount { get; init; }
    public List<string> DiagnosticKinds { get; init; } = [];
}

public sealed class WikiP3Report
{
    public string GeneratedAt { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public WikiP3Startup Startup { get; init; } = new();
    public WikiP3Sync Sync { get; init; } = new();
    public WikiP3Image Images { get; init; } = new();
    public WikiP3Flag Flag { get; init; } = new();
    public WikiP3DiagnosticsPage DiagnosticsPage { get; init; } = new();
    public List<string> Failures { get; init; } = [];
    public bool Pass => Failures.Count == 0;
}

public static class WikiP3Harness
{
    private const int ImageSampleSize = 60;

    public static async Task<WikiP3Report> RunAsync(string distRoot, string cacheRoot, IWikiFlagStore flagStore)
    {
        List<string> failures = [];
        string stamp = DateTime.UtcNow.ToString("O");

        // ── flag：默认 legacy、持久化、重启保持、回滚、脏值 ──
        WikiSourceFeatureFlag flag = new(flagStore);
        bool defaultLegacy = flag.IsLegacyMode;
        flag.Switch(WikiSourceFeatureFlag.EnemyWeb753);
        bool persisted = string.Equals(flagStore.Read(), WikiSourceFeatureFlag.EnemyWeb753, StringComparison.Ordinal);
        bool restartKeeps = new WikiSourceFeatureFlag(flagStore).IsWebMode;
        flag.Switch(WikiSourceFeatureFlag.Legacy722Bundled);
        bool rollbackPersisted = new WikiSourceFeatureFlag(flagStore).IsLegacyMode;
        string? saved = flagStore.Read();
        flagStore.Write("garbage");
        bool dirtyLegacy = new WikiSourceFeatureFlag(flagStore).IsLegacyMode;
        flagStore.Write(saved ?? WikiSourceFeatureFlag.Legacy722Bundled);

        if (!defaultLegacy) failures.Add("默认不是 legacy");
        if (!persisted) failures.Add("切换未持久化");
        if (!restartKeeps) failures.Add("重启后未保持 enemy-web-753");
        if (!rollbackPersisted) failures.Add("回滚未持久化");
        if (!dirtyLegacy) failures.Add("脏设置未回落到 legacy");

        WikiP3Flag flagReport = new()
        {
            DefaultSource = WikiSourceFeatureFlag.Legacy722Bundled,
            DefaultIsLegacy = defaultLegacy,
            SwitchingPersisted = persisted,
            RestartKeepsWeb = restartKeeps,
            RollbackPersisted = rollbackPersisted,
            DirtyValueFallsBackToLegacy = dirtyLegacy,
        };

        // ── 冷启动：无缓存。legacy 与 web 都不该等网络 ──
        WikiDiagnosticsLog coldLog = new();
        WikiSourceFeatureFlag coldFlag = new(new MemoryFlagStore(WikiSourceFeatureFlag.Legacy722Bundled));
        WikiSyncCoordinator coldCoordinator = new(coldFlag, coldLog, _ => throw new InvalidOperationException("冷启动不得触网"));
        WikiSyncTiming legacyPrime = await coldCoordinator.PrimeAsync(null).ConfigureAwait(false);

        coldFlag.Switch(WikiSourceFeatureFlag.EnemyWeb753);
        WikiSyncTiming coldWebPrime = await coldCoordinator.PrimeAsync(null).ConfigureAwait(false);
        string coldFallback = coldCoordinator.State.FallbackReason ?? string.Empty;
        if (string.IsNullOrEmpty(coldFallback)) failures.Add("冷启动无缓存未记录 fallbackReason");

        // ── 首次同步：真实 HTTP（本地镜像 handler），空缓存 ──
        string syncCacheRoot = Path.Combine(cacheRoot, "sync-" + Guid.NewGuid().ToString("N"));
        using LocalMirrorHandler firstHandler = new(distRoot);
        HttpWikiSourceOptions firstOptions = new()
        {
            Endpoint = "https://ellen.wiki",
            Handler = firstHandler,
            CacheRoot = syncCacheRoot,
            MinRequestInterval = TimeSpan.Zero,
            MaxRetries = 1,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        };
        WikiCacheStore firstCache = new(syncCacheRoot);
        WikiDiagnosticsLog syncLog = new();
        using HttpLauncherSnapshotSource firstSnapshot = new(firstOptions, firstCache, syncLog);
        using HttpEnemyWebSource firstSource = new(firstOptions, firstCache, syncLog, firstSnapshot);

        EnemyWebReadResult? firstResult = null;
        WikiSourceFeatureFlag syncFlag = new(new MemoryFlagStore(WikiSourceFeatureFlag.EnemyWeb753));
        WikiSyncCoordinator syncCoordinator = new(
            syncFlag,
            syncLog,
            async token => firstResult = await firstSource.ReadAsync(token).ConfigureAwait(false),
            () => firstSource.Stats,
            () => firstSnapshot.Stats);

        await syncCoordinator.PrimeAsync(null).ConfigureAwait(false);
        Stopwatch firstWatch = Stopwatch.StartNew();
        WikiSyncState firstState = await syncCoordinator.SyncNowAsync().ConfigureAwait(false);
        firstWatch.Stop();

        if (firstState.ItemCount != EnemyItemSchema.ExpectedItemCount)
        {
            failures.Add($"首次同步 DTO 级条目 {firstState.ItemCount}/{EnemyItemSchema.ExpectedItemCount}");
        }

        if (firstState.Timing.StartupBlockedBySync) failures.Add("同步阻塞了启动");
        if (firstResult is null) failures.Add("首次同步未返回结果");

        // ── 热启动：同一缓存目录，真实本地 IO 计时 ──
        WikiCacheStore warmCache = new(syncCacheRoot);
        WikiDiagnosticsLog warmLog = new();
        WikiSourceFeatureFlag warmFlag = new(new MemoryFlagStore(WikiSourceFeatureFlag.EnemyWeb753));
        using LocalMirrorHandler warmHandler = new(distRoot);
        HttpWikiSourceOptions warmOptions = new()
        {
            Endpoint = "https://ellen.wiki",
            Handler = warmHandler,
            CacheRoot = syncCacheRoot,
            MinRequestInterval = TimeSpan.Zero,
            MaxRetries = 1,
        };
        using HttpLauncherSnapshotSource warmSnapshot = new(warmOptions, warmCache, warmLog);
        using HttpEnemyWebSource warmSource = new(warmOptions, warmCache, warmLog, warmSnapshot);
        WikiSyncCoordinator warmCoordinator = new(
            warmFlag,
            warmLog,
            token => warmSource.ReadAsync(token),
            () => warmSource.Stats,
            () => warmSnapshot.Stats);

        // 首屏真实需要的是 manifest + index（列表页用 index 摘要即可渲染，item 在详情页才读）。
        // 另测「读全部 753 条 item」作为详情页全量预热成本，两者分开报，避免首屏数字被放大。
        WikiSyncTiming hotPrime = await warmCoordinator.PrimeAsync(token => Task.Run(() =>
        {
            int n = 0;
            if (warmCache.ReadBytes("manifest.json") is not null) n++;
            if (warmCache.ReadBytes("enemy-index.json") is not null) n++;
            _ = warmCache.ManifestDigest;
            return n;
        }, token)).ConfigureAwait(false);

        Stopwatch fullItemsWatch = Stopwatch.StartNew();
        int fullItemsRead = 0;
        foreach (string key in warmCache.CachedKeys().Where(k => k.StartsWith("enemy:", StringComparison.Ordinal)))
        {
            if (warmCache.ReadBytes(key) is not null) fullItemsRead++;
        }

        fullItemsWatch.Stop();

        Stopwatch incWatch = Stopwatch.StartNew();
        WikiSyncState incState = await warmCoordinator.SyncNowAsync().ConfigureAwait(false);
        incWatch.Stop();

        if (incState.Cache.Downloads != 0) failures.Add($"增量同步仍下载了 {incState.Cache.Downloads} 个文件");
        if (incState.ItemCount != EnemyItemSchema.ExpectedItemCount)
        {
            failures.Add($"增量同步条目 {incState.ItemCount}/{EnemyItemSchema.ExpectedItemCount}");
        }

        WikiP3Startup startup = new()
        {
            ColdStartLegacyFirstScreenMs = legacyPrime.FirstScreenMs,
            ColdStartWebFirstScreenMs = coldWebPrime.FirstScreenMs,
            HotStartWebFirstScreenMs = hotPrime.FirstScreenMs,
            HotStartFullItemsMs = Math.Round(fullItemsWatch.Elapsed.TotalMilliseconds, 3),
            HotStartFullItemsRead = fullItemsRead,
            StartupBlockedBySync = firstState.Timing.StartupBlockedBySync || incState.Timing.StartupBlockedBySync,
            ColdWebFallbackReason = coldFallback,
            HotStartCachedItems = incState.ItemCount,
        };

        WikiP3Sync sync = new()
        {
            FirstSyncMs = Math.Round(firstWatch.Elapsed.TotalMilliseconds, 3),
            IncrementalSyncMs = Math.Round(incWatch.Elapsed.TotalMilliseconds, 3),
            FirstSyncDownloads = firstState.Cache.Downloads,
            FirstSyncRequests = firstHandler.RequestCount,
            IncrementalDownloads = incState.Cache.Downloads,
            IncrementalNotModified = incState.Cache.NotModified,
            IncrementalRequests = warmHandler.RequestCount,
            ItemsIncremental = incState.ItemCount,
        };

        // ── 图片按需缓存：真实下载一批，再解析一次看命中 ──
        List<WikiImageRef> imageRefs = firstResult is null
            ? []
            : CollectImageRefs(firstResult, ImageSampleSize);
        WikiP3Image images = await MeasureImagesAsync(distRoot, syncCacheRoot, imageRefs).ConfigureAwait(false);
        if (images.Sampled == 0) failures.Add("图片样本为空");

        WikiDiagnosticsSummary diagSummary = incState.Summary;
        WikiP3DiagnosticsPage diag = new()
        {
            Source = incState.Source,
            Digest = incState.Digest,
            GeneratedAt = incState.GeneratedAt,
            FallbackReason = incState.FallbackReason,
            ItemCount = incState.ItemCount,
            SourceStateText = diagSummary.SourceStateText,
            FieldWarningText = diagSummary.FieldWarningText,
            C1Cache = diagSummary.C1Cache,
            C2Cache = diagSummary.C2Cache,
            C1CacheText = diagSummary.C1CacheText,
            C2CacheText = diagSummary.C2CacheText,
            CacheHitRate = incState.Cache.HitRate,
            CacheNotModified = incState.Cache.NotModified,
            CacheDownloads = incState.Cache.Downloads,
            ImageFailureRate = images.FailureRate,
            DiagnosticsCount = incState.Diagnostics.Count,
            DiagnosticKinds = [.. incState.Diagnostics.Select(d => d.Kind).Distinct().OrderBy(k => k, StringComparer.Ordinal)],
        };

        if (string.IsNullOrEmpty(diag.Digest)) failures.Add("诊断页缺少 manifest digest");
        if (diagSummary.SourceDegraded) failures.Add($"正常同步出现源降级：{diagSummary.SourceFallbackReason}");
        if (diagSummary.FieldWarnings.Count == 0) failures.Add("预期至少有 curve-strict-alert 字段告警");
        if (diagSummary.C1Cache.Downloads == 0 && diagSummary.C1Cache.NotModified == 0) failures.Add("C1 缓存统计为空，C1/C2 未分开");

        return new WikiP3Report
        {
            GeneratedAt = stamp,
            Note = "真实 Stopwatch + 可注入 HttpMessageHandler（本地 dist 镜像）；UI 渲染耗时不在测量范围；图片为按需缓存抽样（P3 不宣称离线可用）。",
            Startup = startup,
            Sync = sync,
            Images = images,
            Flag = flagReport,
            DiagnosticsPage = diag,
            Failures = failures,
        };
    }

    private static List<WikiImageRef> CollectImageRefs(EnemyWebReadResult result, int limit)
    {
        List<WikiImageRef> refs = [];
        foreach ((string id, EnemyItemDto dto) in result.Items.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(dto.Icon))
            {
                refs.Add(new WikiImageRef { EnemyId = id, File = dto.Icon! });
            }

            foreach (string file in dto.Images)
            {
                refs.Add(new WikiImageRef { EnemyId = id, File = file });
            }

            if (refs.Count >= limit)
            {
                break;
            }
        }

        return [.. refs.Take(limit)];
    }

    private static async Task<WikiP3Image> MeasureImagesAsync(string distRoot, string cacheRoot, List<WikiImageRef> refs)
    {
        using LocalMirrorHandler coldHandler = new(distRoot);
        HttpWikiSourceOptions coldOptions = new()
        {
            Endpoint = "https://ellen.wiki",
            Handler = coldHandler,
            CacheRoot = cacheRoot,
            MinRequestInterval = TimeSpan.Zero,
            MaxRetries = 0,
        };
        WikiCacheStore coldCache = new(cacheRoot);
        WikiDiagnosticsLog coldLog = new();
        using HttpEnemyWebSource coldSource = new(coldOptions, coldCache, coldLog);

        Stopwatch coldWatch = Stopwatch.StartNew();
        int failures = 0;
        foreach (WikiImageRef image in refs)
        {
            if (!await coldSource.TryCacheImageAsync(image.EnemyId, image.File).ConfigureAwait(false))
            {
                failures++;
            }
        }

        coldWatch.Stop();
        WikiCacheStats coldStats = coldSource.Stats;

        using LocalMirrorHandler warmHandler = new(distRoot);
        HttpWikiSourceOptions warmOptions = new()
        {
            Endpoint = "https://ellen.wiki",
            Handler = warmHandler,
            CacheRoot = cacheRoot,
            MinRequestInterval = TimeSpan.Zero,
        };
        WikiCacheStore warmCache = new(cacheRoot);
        WikiDiagnosticsLog warmLog = new();
        using HttpEnemyWebSource warmSource = new(warmOptions, warmCache, warmLog);

        Stopwatch warmWatch = Stopwatch.StartNew();
        int warmHits = 0;
        foreach (WikiImageRef image in refs)
        {
            if (await warmSource.TryCacheImageAsync(image.EnemyId, image.File).ConfigureAwait(false))
            {
                warmHits++;
            }
        }

        warmWatch.Stop();

        int sampled = refs.Count;
        return new WikiP3Image
        {
            Sampled = sampled,
            ColdHits = coldStats.Hits,
            ColdMisses = coldStats.Misses,
            ColdFailures = failures,
            WarmHits = warmHits,
            ColdHitRate = sampled == 0 ? 0 : Math.Round(100.0 * coldStats.Hits / sampled, 2),
            WarmHitRate = sampled == 0 ? 0 : Math.Round(100.0 * warmHits / sampled, 2),
            FailureRate = sampled == 0 ? 0 : Math.Round(100.0 * failures / sampled, 2),
            ColdDownloadMs = Math.Round(coldWatch.Elapsed.TotalMilliseconds, 3),
            WarmResolveMs = Math.Round(warmWatch.Elapsed.TotalMilliseconds, 3),
        };
    }

    private sealed class MemoryFlagStore(string? initial) : IWikiFlagStore
    {
        private string? value = initial;

        public string? Read() => value;

        public void Write(string value) => this.value = value;
    }
}
