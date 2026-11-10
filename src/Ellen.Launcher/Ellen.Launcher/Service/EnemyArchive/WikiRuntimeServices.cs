// P3 运行时接线：flag 持久化、HTTP 源、增量缓存、后台同步协调器。
// 默认 flag = legacy-722-bundled，这些服务注册但不触发网络；切 enemy-web-753 才生效。

using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Domain.WikiSource;

namespace Ellen.Launcher.Service.EnemyArchive;

/// <summary>把 flag 持久化到主项目设置库（Domain 不反向依赖）。</summary>
public sealed class LocalSettingWikiFlagStore : IWikiFlagStore
{
    public string? Read()
    {
        string value = LocalSetting.Get<string>(SettingKeys.WikiEnemyUiSource, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Write(string value)
    {
        LocalSetting.Set(SettingKeys.WikiEnemyUiSource, value);
    }
}

/// <summary>
/// P5a 灰度指标：会话数、web 装配耗时、回退率、图片/同步失败数。
/// 落在缓存根目录 rollout-metrics.json，供灰度期回收；不记录任何用户内容。
/// </summary>
[Service(ServiceLifetime.Singleton)]
public sealed class WikiRolloutMetricsService
{
    /* G7a 证据的构建归属：每条 session 记下产生它的客户端版本。
       换包后 gate 能看出「这四条是旧包跑的还是新包跑重跑的」，
       否则 G6/G7b 来自新包、G7a 来自旧包，审计链上「同一构建」就断了。 */
    private static readonly string AppVersion =
        typeof(WikiRolloutMetricsService).Assembly.GetName().Version?.ToString() ?? string.Empty;

    private readonly WikiCacheStore cache;
    private readonly WikiSourceFeatureFlag flag;
    private readonly WikiRolloutMetrics metrics;

    public WikiRolloutMetricsService(WikiCacheStore cache, WikiSourceFeatureFlag flag)
    {
        this.cache = cache;
        this.flag = flag;
        metrics = WikiRolloutMetricsStore.Load(cache.Root);
        metrics.InstallId = flag.InstallId;
        metrics.RecordSession(DateTimeOffset.UtcNow.ToString("O"));
        WikiRolloutMetricsStore.Save(cache.Root, metrics);

        // W2-1：首次运行落 0 基线，使「0 崩溃」与「未埋点」可区分（缺失=MANUAL）。
        WikiRolloutMetricsStore.EnsureCrashRecord(cache.Root);
    }

    public WikiRolloutMetrics Metrics => metrics;

    public string RolloutText =>
        $"灰度：{flag.RolloutReason} / 生效 {flag.UiSource} / 启动时 {flag.StartupUiSource}"
        + $" / 持久化 {(string.IsNullOrEmpty(flag.PersistedUiSource) ? "（未手动切换过）" : flag.PersistedUiSource)}"
        + $" / installId {flag.InstallId}";

    public void RecordDecision(WikiFirstRunDecision decision)
    {
        metrics.RecordDecision(decision);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }

    /// <summary>
    /// 753 视图装配完成后调用。除了计数，再落一条 G7a 行为探针（W1-5/6/8）：
    /// 随包命中 / legacy 并存 / 回滚 / 切换重启保持四项由 <see cref="WikiOfflineProbeStore"/>
    /// 按 sessions 推导，**不再靠人工手写 rollout-probe-offline.json**。
    /// </summary>
    public void RecordWebLoad(double milliseconds, int itemCount, bool offlineBundled)
    {
        metrics.RecordWebLoad(milliseconds, itemCount);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
        _ = WikiOfflineProbeStore.Record(
            cache.Root,
            flag.InstallId,
            flag.UiSource,
            flag.PersistedUiSource,
            flag.StartupUiSource,
            flag.IsGrayscale,
            offlineBundled,
            itemCount,
            metrics.WebFallbacks,
            AppVersion);
    }

    /// <summary>
    /// legacy 722 视图装配完成后调用。G7a 的「legacy 并存 / 回滚」必须有 legacy 侧证据，
    /// 只在 web 侧记录会让这两项永远 false。这里显式写 <c>legacy-722-bundled</c>：
    /// 即使灰度决议是 web、实际却回退到 legacy，记录的也应该是**实际生效的视图**。
    /// </summary>
    public void RecordLegacyLoad(int itemCount)
    {
        _ = WikiOfflineProbeStore.Record(
            cache.Root,
            flag.InstallId,
            WikiSourceFeatureFlag.Legacy722Bundled,
            flag.PersistedUiSource,
            flag.StartupUiSource,
            flag.IsGrayscale,
            offlineBundled: false,
            itemCount,
            metrics.WebFallbacks,
            AppVersion);
    }

    public void RecordImageFailures(int count)
    {
        metrics.RecordImageFailures(count);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }

    /// <summary>
    /// UI 层真实下载（ImageCache 管线）增量记账：成功 / 失败分开。
    /// 补的是旧口径盲区 —— 下载失败在 CachedImage 里被 catch 成占位图，过去既不进 metrics 也不进 Sentry。
    /// 同时把 downloads + failures 计入 <c>ImageAttempts</c> 分母；只加分子不加分母 = 用解析层
    /// 十万级分母把 UI 失败率稀释成 0.0x%，是名义覆盖、实际盲区。
    /// </summary>
    public void RecordImageUiDownloads(int downloads, int failures)
    {
        if (downloads <= 0 && failures <= 0)
        {
            return;
        }

        metrics.RecordImageUiDownloads(downloads, failures);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }

    /// <summary>图片网络失败（仅下载失败），网络口径分子；仅诊断，不参与 gate 判定。</summary>
    public void RecordImageNetworkFailures(int count)
    {
        if (count <= 0)
        {
            return;
        }

        metrics.RecordImageNetworkFailures(count);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }

    /// <summary>图片解析尝试（分母）；与失败数一起用于灰度期图片失败率。</summary>
    public void RecordImageAttempts(int count)
    {
        if (count <= 0)
        {
            return;
        }

        metrics.RecordImageAttempts(count);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }

    /// <summary>P6 随包口径：随包原图命中 / 离线缩略图预览（③④ 取证用，不进失败率分母）。</summary>
    public void RecordBundledImageStats(int hits, int thumbPreviews)
    {
        if (hits <= 0 && thumbPreviews <= 0)
        {
            return;
        }

        metrics.RecordBundledImageStats(hits, thumbPreviews);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }

    /// <summary>图片网络下载尝试（= cache miss）；单列用于诊断纯网络失败率，不参与 gate 判定。</summary>
    public void RecordImageNetworkAttempts(int count)
    {
        if (count <= 0)
        {
            return;
        }

        metrics.RecordImageNetworkAttempts(count);
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }

    public void RecordSyncFailure()
    {
        metrics.RecordSyncFailure();
        WikiRolloutMetricsStore.Save(cache.Root, metrics);
    }
}

/// <summary>诊断页数据源：聚合 source / digest / generatedAt / fallback / 图片失败率 / 缓存命中率。</summary>
public interface IWikiDiagnosticsProvider
{
    WikiSyncState Current { get; }
    void Refresh(WikiSyncState state);
}

[Service(ServiceLifetime.Singleton, typeof(IWikiDiagnosticsProvider))]
public sealed partial class WikiDiagnosticsProvider : IWikiDiagnosticsProvider
{
    private WikiSyncState current = new();

    [GeneratedConstructor]
    public partial WikiDiagnosticsProvider(IServiceProvider serviceProvider);

    public WikiSyncState Current => current;

    public void Refresh(WikiSyncState state)
    {
        current = state;
    }
}
