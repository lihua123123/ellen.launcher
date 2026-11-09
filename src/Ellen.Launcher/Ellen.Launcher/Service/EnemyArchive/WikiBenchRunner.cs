// P5 性能实测（数据路径）：--wiki-bench=<out.json>
// 在真实安装运行时里跑，不 mock：真实缓存、753 items、真实图片解码、真实 alias 深链。
// 只测数据/装配路径；帧级指标（首帧、滚动）由 StartupProbe / UI 探针负责，二者分开报告，不混算。

using System.Diagnostics;
using System.IO;
using Ellen.Launcher.Domain.WikiSource;
using WinRT;

namespace Ellen.Launcher.Service.EnemyArchive;

public static class WikiBenchRunner
{
    public const string ArgumentPrefix = "--wiki-bench=";

    public static async Task<int> RunAsync(string outputPath)
    {
        Stopwatch total = Stopwatch.StartNew();
        Process process = Process.GetCurrentProcess();
        double sinceProcessStart = Math.Max(0, (DateTime.Now - process.StartTime).TotalMilliseconds);

        ComWrappersSupport.InitializeComWrappers();
        using ServiceProvider provider = DependencyInjection.Initialize(true);

        WikiSourceFeatureFlag flag = provider.GetRequiredService<WikiSourceFeatureFlag>();
        WikiCacheStore cache = provider.GetRequiredService<WikiCacheStore>();
        HttpWikiSourceOptions options = provider.GetRequiredService<HttpWikiSourceOptions>();
        IEnemyWebSource enemy = provider.GetRequiredService<IEnemyWebSource>();
        IWikiEnemyCatalogService catalogService = provider.GetRequiredService<IWikiEnemyCatalogService>();

        cache.SweepTempFiles();

        // M2：纯缓存首屏（不触网）
        Stopwatch watch = Stopwatch.StartNew();
        EnemyWebReadResult? cached = enemy is ICachedEnemyWebSource cachedSource
            ? await cachedSource.ReadCachedAsync().ConfigureAwait(false)
            : null;
        double cachedMs = watch.Elapsed.TotalMilliseconds;
        int cachedCount = cached?.Items.Count ?? 0;

        // 无缓存时做一次真实同步（HTTP 或本地镜像），并记录耗时
        double firstSyncMs = 0;
        int firstSyncCount = cachedCount;
        if (cachedCount == 0)
        {
            watch.Restart();
            EnemyWebReadResult full = await enemy.ReadAsync().ConfigureAwait(false);
            firstSyncMs = watch.Elapsed.TotalMilliseconds;
            firstSyncCount = full.Items.Count;
        }

        // M3：753 条装配（含分类/搜索 token/图片解析）
        watch.Restart();
        WikiEnemyCatalogResult? catalog = await catalogService.LoadAsync().ConfigureAwait(false);
        double catalogMs = watch.Elapsed.TotalMilliseconds;

        // M5：分类 + 关键词筛选
        IReadOnlyList<WikiEnemyViewItem> items = catalog?.Items ?? [];
        string? category = catalog?.Categories.Skip(1).FirstOrDefault()?.Key;
        double filterCategoryMs = MeasureAverage(() => WikiEnemyCatalog.Filter(items, category, null, []), 10);
        double filterSearchMs = MeasureAverage(() => WikiEnemyCatalog.Filter(items, null, null, ["火"]), 10);

        // M6：深链（全部旧 ID）
        IReadOnlyList<AliasRecord> aliases = catalogService.Aliases;
        WikiDeepLink deepLink = new(new AliasResolver(aliases));
        string[] oldIds = [.. aliases.Select(a => a.OldId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal)];
        int degraded = 0;
        watch.Restart();
        foreach (string oldId in oldIds)
        {
            (string slug, _, _, bool isDegraded) = deepLink.Resolve(oldId, null);
            if (isDegraded || string.IsNullOrEmpty(slug))
            {
                degraded++;
            }
        }

        double deepLinkMs = watch.Elapsed.TotalMilliseconds;

        // P5：验证图片链路（正式 manifest 缺失时如实记录，不假装离线）
        double imageSyncMs = 0;
        int imageSynced = 0;
        int imageSyncFailed = 0;
        int imageHashRejected = 0;
        bool manifestAvailable = false;
        bool imageSyncAttempted = enemy is HttpEnemyWebSource;
        if (enemy is HttpEnemyWebSource httpEnemy)
        {
            WikiAssetManifest? manifest = await httpEnemy.ReadAssetsManifestAsync().ConfigureAwait(false);
            manifestAvailable = manifest is not null;
            if (manifest is not null)
            {
                watch.Restart();
                WikiImageSyncResult sync = await httpEnemy.SyncImagesAsync(manifest, maxFiles: 120, concurrency: 6).ConfigureAwait(false);
                imageSyncMs = watch.Elapsed.TotalMilliseconds;
                imageSynced = sync.Downloaded + sync.AlreadyCached;
                imageSyncFailed = sync.Failed;
                imageHashRejected = sync.HashRejected;
            }
        }

        // M9：真实图片解码（缓存里的前 60 张；无缓存则 0 并如实标注）
        (int decoded, double decodeMs, double decodeAvg, double decodeMax) = await DecodeCachedImagesAsync(cache, 60).ConfigureAwait(false);

        // M10：纯缓存冷启动（同一进程内不再触网；等效离线数据路径，进程级离线另跑一次）
        watch.Restart();
        EnemyWebReadResult? offline = enemy is ICachedEnemyWebSource offlineSource
            ? await offlineSource.ReadCachedAsync().ConfigureAwait(false)
            : null;
        double offlineMs = watch.Elapsed.TotalMilliseconds;

        IReadOnlyList<WikiCacheTierUsage> tiers = cache.GetTierUsage(options.EffectiveTierLimits);
        long cacheBytes = tiers.Sum(t => t.Bytes);

        var result = new
        {
            schema = "wiki-bench-v1",
            generatedAt = DateTimeOffset.Now.ToString("O"),
            runtime = new
            {
                endpoint = options.Endpoint,
                cacheRoot = cache.Root,
                mode = Environment.GetEnvironmentVariable("ELLEN_WIKI_RUNTIME") ?? "http",
                uiSource = flag.UiSource,
                rollout = flag.RolloutReason,
                installId = flag.InstallId,
                onlineFlag = !string.IsNullOrEmpty(flag.InstallId),
            },
            M1_processStartToBenchMs = Math.Round(sinceProcessStart, 1),
            M2_cachedFirstScreen = new { ms = Math.Round(cachedMs, 3), items = cachedCount },
            M2b_firstSync = new { ms = Math.Round(firstSyncMs, 3), items = firstSyncCount, skipped = cachedCount > 0 },
            M3_catalogBuild = new { ms = Math.Round(catalogMs, 3), items = items.Count, categories = catalog?.Categories.Count ?? 0 },
            M3b_catalogInternal = new
            {
                assemblyMs = catalog?.LoadMilliseconds ?? 0,
                filterMs = catalog?.FilterMilliseconds ?? 0,
                imageResolveMs = catalog?.ImageResolveMilliseconds ?? 0,
            },
            M5_filter = new { categoryMs = Math.Round(filterCategoryMs, 3), searchMs = Math.Round(filterSearchMs, 3) },
            M6_deepLink = new { count = oldIds.Length, ms = Math.Round(deepLinkMs, 3), degraded },
            P5_imageSync = new
            {
                manifestAvailable,
                ms = Math.Round(imageSyncMs, 3),
                synced = imageSynced,
                failed = imageSyncFailed,
                hashRejected = imageHashRejected,
                /* 说明必须与事实一致：A4 早已交付 manifest，这里 false 只可能是「本次没走 HTTP 源」
                   或「dist 里真的缺文件」。旧文案把两种情况都写成「上游未提供」，会误导读者以为 A4 未交付。 */
                note = manifestAvailable
                    ? "assets manifest 可用，已按 sha256 校验同步"
                    : (imageSyncAttempted
                        ? "HTTP 源模式下未读到 assets manifest（检查 dist/api/enemies/assets-manifest.json）"
                        : "本次运行时不是 HTTP 源（如 ELLEN_WIKI_RUNTIME=file），图片同步未测；不代表 manifest 缺失"),
            },
            M9_imageDecode = new
            {
                count = decoded,
                ms = Math.Round(decodeMs, 3),
                avgMs = Math.Round(decodeAvg, 3),
                maxMs = Math.Round(decodeMax, 3),
                note = decoded == 0
                    ? (imageSyncAttempted
                        ? "缓存无图片：先跑一次在线同步"
                        : "本次未做图片同步（非 HTTP 源），M9 解码样本为 0，不代表解码性能")
                    : "Windows.Graphics.Imaging.BitmapDecoder 真实解码",
            },
            M10_offlineCachedRead = new { ms = Math.Round(offlineMs, 3), items = offline?.Items.Count ?? 0 },
            memory = new
            {
                managedBytes = GC.GetTotalMemory(true),
                workingSetBytes = process.WorkingSet64,
                peakWorkingSetBytes = process.PeakWorkingSet64,
            },
            cache = new
            {
                entries = cache.CachedFileCount,
                bytes = cacheBytes,
                budgetBytes = options.CacheMaxBytes,
                tiers = tiers.Select(t => new { t.Name, t.Files, t.Bytes, t.LimitBytes, t.Protected }),
                recoveryNote = cache.RecoveryNote,
            },
            diagnostics = new
            {
                count = catalog?.Diagnostics.Count ?? 0,
                degradations = catalog?.Diagnostics.Count(d => WikiDiagnosticClassifier.IsSourceDegradation(d.Kind)) ?? 0,
                imageFailures = catalog?.Diagnostics.Count(d => d.Kind == "image-download-failure") ?? 0,
            },
            totalMs = Math.Round(total.Elapsed.TotalMilliseconds, 1),
        };

        string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"[wiki-bench] 写出 {outputPath}");
        Console.WriteLine($"[wiki-bench] 缓存 {cachedCount} / 首同步 {firstSyncMs:0.#}ms / 装配 {catalogMs:0.#}ms / 筛选 {filterCategoryMs:0.##}ms / 深链 {oldIds.Length} 条 {degraded} 降级 / 图片解码 {decoded} 张 {decodeAvg:0.##}ms/张");
        return 0;
    }

    private static double MeasureAverage(Action action, int iterations)
    {
        action();
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            action();
        }

        return watch.Elapsed.TotalMilliseconds / iterations;
    }

    private static async Task<(int Count, double TotalMs, double AvgMs, double MaxMs)> DecodeCachedImagesAsync(WikiCacheStore cache, int limit)
    {
        string[] keys = [.. cache.CachedKeys()
            .Where(k => k.StartsWith("image:", StringComparison.Ordinal))
            .Take(limit * 3)];

        int count = 0;
        double total = 0;
        double max = 0;
        Stopwatch watch = new();
        foreach (string key in keys)
        {
            if (count >= limit)
            {
                break;
            }

            string path = cache.FilePath(key);
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                watch.Restart();
                Windows.Storage.StorageFile file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
                using Windows.Storage.Streams.IRandomAccessStream stream = await file.OpenReadAsync();
                Windows.Graphics.Imaging.BitmapDecoder decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
                _ = await decoder.GetPixelDataAsync();
                watch.Stop();
                total += watch.Elapsed.TotalMilliseconds;
                max = Math.Max(max, watch.Elapsed.TotalMilliseconds);
                count++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 解码失败不算性能样本，如实少计数（报告里 count 会小于请求数）。
            }
        }

        return (count, total, count == 0 ? 0 : total / count, max);
    }
}
