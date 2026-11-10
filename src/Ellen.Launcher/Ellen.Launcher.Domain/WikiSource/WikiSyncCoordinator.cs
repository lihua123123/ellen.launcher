// P3 启动不阻塞：首屏只读本地缓存/legacy，HTTP 同步在后台跑。
// 真实 Stopwatch 计时（冷启动 / 热启动 / 首次同步 / 增量同步），供诊断页与验收报告。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class WikiSyncTiming
{
    public double FirstScreenMs { get; init; }
    public double FirstSyncMs { get; init; }
    public double IncrementalSyncMs { get; init; }
    public double HotStartMs { get; init; }
    public bool StartupBlockedBySync { get; init; }
}

public sealed class WikiSyncState
{
    public string Source { get; init; } = string.Empty;
    public string? Digest { get; init; }
    public string? GeneratedAt { get; init; }
    public string? FallbackReason { get; init; }
    public int ItemCount { get; init; }

    /// <summary>C2 敌人源统计（index + 753 items + 图片）。P3 起一直用它。</summary>
    public WikiCacheStats Cache { get; init; } = new();

    /// <summary>C1 manifest/zip 统计；与 C2 分开，避免「754 下载」被误解成含 manifest。</summary>
    public WikiCacheStats SnapshotCache { get; init; } = new();

    public int ImageFailures { get; init; }
    public int ImageTotal { get; init; }
    public double ImageFailureRate => ImageTotal == 0 ? 0 : Math.Round(100.0 * ImageFailures / ImageTotal, 2);
    public WikiSyncTiming Timing { get; init; } = new();
    public IReadOnlyList<WikiDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>诊断页直接消费：源状态 / 字段告警 / C1-C2 分列。</summary>
    public WikiDiagnosticsSummary Summary
    {
        get
        {
            IReadOnlyList<WikiDiagnostic> degradations =
                [.. Diagnostics.Where(d => WikiDiagnosticClassifier.IsSourceDegradation(d.Kind))];
            return new WikiDiagnosticsSummary
            {
                Source = Source,
                // 有降级诊断也算降级（后台同步异常等场景 FallbackReason 可能由调用方单独设置）。
                SourceDegraded = FallbackReason is { Length: > 0 } || degradations.Count > 0,
                SourceFallbackReason = FallbackReason ?? (degradations.Count > 0 ? $"{degradations[0].Kind}: {degradations[0].Detail}" : null),
                SourceDegradations = degradations,
                FieldWarnings = [.. Diagnostics.Where(d => !WikiDiagnosticClassifier.IsSourceDegradation(d.Kind))],
                C1Cache = SnapshotCache,
                C2Cache = Cache,
            };
        }
    }
}

/// <summary>
/// 协调「首屏快路径 + 后台同步」。首屏只做本地缓存读取，不 await 任何网络请求；
/// 同步失败显式降级到缓存/legacy 并记录。默认 flag 仍为 legacy-722-bundled。
/// </summary>
public sealed class WikiSyncCoordinator : IDisposable
{
    private readonly WikiDiagnosticsLog log;
    private readonly WikiSourceFeatureFlag flag;
    private readonly Func<CancellationToken, ValueTask<EnemyWebReadResult>> backgroundSync;
    private readonly Func<WikiCacheStats>? cacheStats;
    private readonly Func<WikiCacheStats>? snapshotCacheStats;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int running;
    private WikiSyncState state = new();

    public WikiSyncCoordinator(
        WikiSourceFeatureFlag flag,
        WikiDiagnosticsLog log,
        Func<CancellationToken, ValueTask<EnemyWebReadResult>> backgroundSync,
        Func<WikiCacheStats>? cacheStats = null,
        Func<WikiCacheStats>? snapshotCacheStats = null)
    {
        this.flag = flag;
        this.log = log;
        this.backgroundSync = backgroundSync;
        this.cacheStats = cacheStats;
        this.snapshotCacheStats = snapshotCacheStats;
    }

    public WikiSyncState State => state;

    /// <summary>
    /// 首屏：只读本地缓存/legacy，绝不等待网络。返回耗时用于冷/热启动对比。
    /// <paramref name="localRead"/> 传入真实的本地缓存读取动作（例如读 manifest + index + 已缓存条目），
    /// 计时才是真实首屏 IO 耗时；传 null 表示没有任何本地数据可读（冷启动）。
    /// </summary>
    public async Task<WikiSyncTiming> PrimeAsync(Func<CancellationToken, Task<int>>? localRead, CancellationToken token = default)
    {
        Stopwatch watch = Stopwatch.StartNew();
        int cached = 0;
        bool cacheAvailable = false;
        if (localRead is not null)
        {
            // 纯本地 IO，不触网；异常一律视为「无缓存」，不阻塞首屏。
            try
            {
                cached = await localRead(token).ConfigureAwait(false);
                cacheAvailable = cached > 0;
            }
            catch (IOException)
            {
                cacheAvailable = false;
            }
            catch (UnauthorizedAccessException)
            {
                cacheAvailable = false;
            }
        }

        watch.Stop();
        double firstScreen = watch.Elapsed.TotalMilliseconds;
        state = new WikiSyncState
        {
            Source = flag.IsWebMode ? (cacheAvailable ? "ellen.wiki (cache)" : "bundled") : "bundled",
            FallbackReason = flag.IsWebMode && !cacheAvailable ? "冷启动无缓存，首屏保持 legacy，后台同步中" : null,
            ItemCount = cached,
            Cache = new WikiCacheStats(),
            Timing = new WikiSyncTiming { FirstScreenMs = Math.Round(firstScreen, 3), StartupBlockedBySync = false },
            Diagnostics = log.Records,
        };
        return state.Timing;
    }

    /// <summary>后台同步：fire-and-forget，不阻塞启动；重复调用自动合并。</summary>
    public void SyncInBackground()
    {
        if (!flag.IsWebMode || Interlocked.Exchange(ref running, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await ExecuteSyncAsync().ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref running, 0);
            }
        });
    }

    /// <summary>
    /// 跑一次同步并等待完成。仅供诊断页「立即同步」与演练使用；
    /// 启动路径走 <see cref="SyncInBackground"/>，不得在首屏 await 本方法。
    /// </summary>
    public async Task<WikiSyncState> SyncNowAsync(CancellationToken token = default)
    {
        if (Interlocked.Exchange(ref running, 1) == 1)
        {
            return state;
        }

        try
        {
            return await ExecuteSyncAsync(token).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref running, 0);
        }
    }

    private async Task<WikiSyncState> ExecuteSyncAsync(CancellationToken token = default)
    {
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                EnemyWebReadResult result = await backgroundSync(token).ConfigureAwait(false);
                watch.Stop();
                WikiCacheStats stats = cacheStats?.Invoke() ?? new WikiCacheStats();
                WikiCacheStats snapshotStats = snapshotCacheStats?.Invoke() ?? new WikiCacheStats();
                // 「增量」按真实网络行为判定：没有新下载且大量 304 → 增量同步。
                bool incremental = stats.Downloads == 0 && stats.NotModified > 0;
                // 只有真正的「数据降级」才算 fallback；曲线告警、计数核对等属提示，不算降级。
                string? degradation = WikiDiagnosticClassifier.FirstDegradationReason(result.Diagnostics);
                state = new WikiSyncState
                {
                    Source = "ellen.wiki (http)",
                    Digest = result.Stamp.Digest,
                    GeneratedAt = result.Stamp.GeneratedAt,
                    ItemCount = result.Items.Count,
                    FallbackReason = degradation,
                    Cache = stats,
                    SnapshotCache = snapshotStats,
                    Timing = new WikiSyncTiming
                    {
                        FirstScreenMs = state.Timing.FirstScreenMs,
                        FirstSyncMs = incremental ? 0 : Math.Round(watch.Elapsed.TotalMilliseconds, 3),
                        IncrementalSyncMs = incremental ? Math.Round(watch.Elapsed.TotalMilliseconds, 3) : 0,
                        StartupBlockedBySync = false,
                    },
                    Diagnostics = result.Diagnostics,
                };
                return state;
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Add("background-sync-failure", WikiSourceStamp.FromBundled("后台同步异常"),
                ex.Message, "后台同步失败，首屏保持缓存/legacy");
            state = new WikiSyncState
            {
                Source = "bundled",
                FallbackReason = $"后台同步失败：{ex.Message}",
                Cache = cacheStats?.Invoke() ?? new WikiCacheStats(),
                SnapshotCache = snapshotCacheStats?.Invoke() ?? new WikiCacheStats(),
                Timing = new WikiSyncTiming { FirstScreenMs = state.Timing.FirstScreenMs, StartupBlockedBySync = false },
                Diagnostics = log.Records,
            };
            return state;
        }
    }

    public void Dispose() => gate.Dispose();
}
