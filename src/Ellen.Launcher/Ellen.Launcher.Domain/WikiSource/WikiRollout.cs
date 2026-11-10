// P5 准备：首次无缓存断网行为 + P5a 灰度白名单 + 运行指标。
// 纯 net10.0，不依赖 UI；VM 只消费决策结果，不自己判断“要不要回退”。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ellen.Launcher.Domain.WikiSource;

/// <summary>首屏数据形态：正常 web / 显式回退 legacy / 空态（同步中，不白屏）。</summary>
public enum WikiFirstRunMode
{
    /// <summary>web 模式且缓存可用（离线也可用缓存）。</summary>
    Web,

    /// <summary>web 模式但无缓存且不可达 → 显示内置 722，并在顶部横幅说明原因。</summary>
    LegacyFallback,

    /// <summary>web 模式、无缓存、正在后台同步 → 显示空态 + 重试按钮，不白屏。</summary>
    EmptyState,
}

public sealed class WikiFirstRunInputs
{
    /// <summary>feature flag 是否指向 enemy-web-753。</summary>
    public bool WebEnabled { get; init; }

    /// <summary>纯缓存读取到的条目数（0 = 无缓存）。</summary>
    public int CachedItemCount { get; init; }

    /// <summary>本次启动是否已确认可达（后台同步成功 / manifest 读取成功）。</summary>
    public bool Online { get; init; }

    /// <summary>失败原因（用于横幅，不编造）。</summary>
    public string? FailureReason { get; init; }
}

public sealed class WikiFirstRunDecision
{
    public WikiFirstRunMode Mode { get; init; }

    /// <summary>顶部横幅文案；<see langword="null"/> 表示不显示。</summary>
    public string? Banner { get; init; }

    /// <summary>机器可读原因（诊断页/指标用）。</summary>
    public string Reason { get; init; } = string.Empty;

    public bool IsFallback => Mode == WikiFirstRunMode.LegacyFallback;
}

/// <summary>
/// 首次运行决策。规则刻意保守：
/// 有缓存 → 用缓存（离线也能看 753）；无缓存但可能在线 → 空态等待同步；无缓存且确定不可达 → 回 legacy 并显式说明。
/// 任何分支都不允许返回“空白页”。
/// </summary>
public static class WikiFirstRunPolicy
{
    public static WikiFirstRunDecision Decide(WikiFirstRunInputs inputs)
    {
        if (!inputs.WebEnabled)
        {
            return new WikiFirstRunDecision { Mode = WikiFirstRunMode.Web, Reason = "web-disabled" };
        }

        if (inputs.CachedItemCount > 0)
        {
            return new WikiFirstRunDecision
            {
                Mode = WikiFirstRunMode.Web,
                Reason = "cache-hit",
                Banner = inputs.Online ? null : $"离线：使用本地缓存的 {inputs.CachedItemCount} 条敌人数据（ellen.wiki 暂不可达）",
            };
        }

        if (inputs.Online)
        {
            return new WikiFirstRunDecision
            {
                Mode = WikiFirstRunMode.EmptyState,
                Reason = "syncing",
                Banner = "首次启动：正在从 ellen.wiki 同步图鉴，完成后自动刷新",
            };
        }

        string detail = string.IsNullOrWhiteSpace(inputs.FailureReason) ? "网络不可达且本地无缓存" : inputs.FailureReason;
        return new WikiFirstRunDecision
        {
            Mode = WikiFirstRunMode.LegacyFallback,
            Reason = "no-cache-offline",
            Banner = $"内置随包图鉴读取失败（{detail}），当前显示内置 722 图鉴；重新安装可恢复",
        };
    }
}

public enum WikiRolloutMode
{
    Off,
    Whitelist,
    All,
}

public sealed class WikiRolloutDecision
{
    /// <summary>最终生效的 feature flag 值。</summary>
    public string Source { get; init; } = WikiSourceFeatureFlag.Legacy722Bundled;

    public bool IsWeb => string.Equals(Source, WikiSourceFeatureFlag.EnemyWeb753, StringComparison.Ordinal);

    /// <summary>是否属于灰度放量（whitelist 命中），而不是全量默认。</summary>
    public bool IsGrayscale { get; init; }

    public string Reason { get; init; } = string.Empty;

    public string InstallId { get; init; } = string.Empty;
}

/// <summary>
/// P5a 灰度：默认 off（沿用用户持久化的 flag），whitelist 只对白名单安装生效，all 才全量。
/// 环境变量：<c>ELLEN_WIKI_ENEMY_ROLLOUT</c> = off|whitelist|all；
/// 白名单文件：缓存根目录下 <c>rollout-whitelist.json</c>（安装 ID 的 SHA256 前 16 位）。
/// </summary>
public static class WikiRolloutPolicy
{
    public const string RolloutEnvironment = "ELLEN_WIKI_ENEMY_ROLLOUT";
    public const string UiSourceOverrideEnvironment = "ELLEN_WIKI_UI_SOURCE";
    public const string WhitelistFileName = "rollout-whitelist.json";

    public static WikiRolloutMode ParseMode(string? value)
    {
        if (string.Equals(value, "whitelist", StringComparison.OrdinalIgnoreCase))
        {
            return WikiRolloutMode.Whitelist;
        }

        if (string.Equals(value, "all", StringComparison.OrdinalIgnoreCase))
        {
            return WikiRolloutMode.All;
        }

        return WikiRolloutMode.Off;
    }

    /// <summary>安装 ID：机器名+用户名哈希，不把原始机器名写进配置/日志。</summary>
    public static string ResolveInstallId(string? machineName = null, string? userName = null)
    {
        string seed = $"{machineName ?? Environment.MachineName}|{userName ?? Environment.UserName}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed)))[..16].ToLowerInvariant();
    }

    public static IReadOnlyCollection<string> ReadWhitelist(string root)
    {
        try
        {
            string path = Path.Combine(root, WhitelistFileName);
            if (!File.Exists(path))
            {
                return [];
            }

            RolloutWhitelistFile? file = JsonSerializer.Deserialize<RolloutWhitelistFile>(File.ReadAllText(path));
            return file?.InstallIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public static WikiRolloutDecision Resolve(
        string? persistedFlag,
        WikiRolloutMode mode,
        string? installId,
        IReadOnlyCollection<string> whitelist,
        string? overrideSource = null)
    {
        string id = string.IsNullOrWhiteSpace(installId) ? ResolveInstallId() : installId;

        // 1) 显式 UI 源覆盖（仅开发/性能演练用）：只接受两个已知值。
        if (string.Equals(overrideSource, WikiSourceFeatureFlag.EnemyWeb753, StringComparison.Ordinal))
        {
            return new WikiRolloutDecision { Source = WikiSourceFeatureFlag.EnemyWeb753, Reason = "ui-source-override", InstallId = id };
        }

        if (string.Equals(overrideSource, WikiSourceFeatureFlag.Legacy722Bundled, StringComparison.Ordinal))
        {
            return new WikiRolloutDecision { Source = WikiSourceFeatureFlag.Legacy722Bundled, Reason = "ui-source-override", InstallId = id };
        }

        // 2) 灰度放量。
        switch (mode)
        {
            case WikiRolloutMode.All:
                return new WikiRolloutDecision
                {
                    Source = WikiSourceFeatureFlag.EnemyWeb753,
                    Reason = "rollout=all",
                    InstallId = id,
                };

            case WikiRolloutMode.Whitelist:
                bool hit = whitelist.Any(w => string.Equals(w, id, StringComparison.OrdinalIgnoreCase));
                return new WikiRolloutDecision
                {
                    Source = hit ? WikiSourceFeatureFlag.EnemyWeb753 : WikiSourceFeatureFlag.Legacy722Bundled,
                    IsGrayscale = hit,
                    Reason = hit ? "rollout=whitelist-hit" : "rollout=whitelist-miss",
                    InstallId = id,
                };

            default:
                // 3) 未放量：只听持久化设置（只接受 enemy-web-753，其余一律 legacy）。
                bool persistedWeb = string.Equals(persistedFlag, WikiSourceFeatureFlag.EnemyWeb753, StringComparison.Ordinal);
                return new WikiRolloutDecision
                {
                    Source = persistedWeb ? WikiSourceFeatureFlag.EnemyWeb753 : WikiSourceFeatureFlag.Legacy722Bundled,
                    Reason = "rollout=off",
                    InstallId = id,
                };
        }
    }

    private sealed class RolloutWhitelistFile
    {
        [JsonPropertyName("installIds")]
        public List<string>? InstallIds { get; set; }
    }
}

/// <summary>
/// P5a 灰度指标：只记计数与最近一次耗时，不记用户内容。
/// 落盘由调用方决定（缓存根目录 rollout-metrics.json），Domain 只负责结构与合并。
/// </summary>
public sealed class WikiRolloutMetrics
{
    public string Schema { get; init; } = "wiki-rollout-metrics-v1";
    public string InstallId { get; set; } = string.Empty;
    public string FirstSessionAt { get; set; } = string.Empty;
    public string LastSessionAt { get; set; } = string.Empty;
    public int Sessions { get; set; }
    public int WebLoads { get; set; }
    public int WebFallbacks { get; set; }
    public int EmptyStates { get; set; }
    public int LastItemCount { get; set; }
    public double LastLoadMs { get; set; }
    public double MaxLoadMs { get; set; }
    /// <summary>
    /// 图片失败总数（与 <see cref="ImageAttempts"/> 同口径：下载失败 + hash 校验失败 + 同步异常 + 无 URL 占位 + UI 解码失败 + UI 层下载失败）。
    /// UI 解码失败仅统计敌人图鉴图源（ellen.wiki 远程图 / Wiki 缓存本地文件），占位图不计。
    /// P6.1 起含 ImageCache 管线的下载失败（<see cref="ImageUiDownloadFailures"/>），该链路以前完全不入账。
    /// </summary>
    public int ImageFailures { get; set; }

    /// <summary>图片网络失败数（仅 <c>image-download-failure</c>），与 <see cref="ImageNetworkAttempts"/> 同口径，仅诊断。</summary>
    public int ImageNetworkFailures { get; set; }

    /// <summary>
    /// 图片处理尝试累计，作图片失败率分母。两部分相加：
    /// ① 解析层引用数（ImageCacheHits+Misses，跨装配累加）；
    /// ② UI 层真实下载尝试（<see cref="ImageUiDownloads"/> + <see cref="ImageUiDownloadFailures"/>）。
    /// ② 必须进分母：P6.1 起 UI 下载失败计入 <see cref="ImageFailures"/>，若分母不含对应尝试，
    /// 分子就来自一条分母没覆盖的链路，比率会被解析层的十万级分母稀释成「名义达标」。
    /// </summary>
    public int ImageAttempts { get; set; }

    /// <summary>
    /// 图片**解析层未命中**累计（= 每次装配的 <c>ImageCacheMisses</c> 之和，跨装配累加）。
    /// ⚠️ 名字有误导：它**不是**网络请求次数 —— 未命中只说明 WikiImageCache 里没有本地文件、返回了在线 URL，
    /// 真正是否发请求由 UI 的 <c>ImageCache</c> 决定（可能命中它自己的 50 MiB 缓存而不下载）。
    /// 真实下载次数看 <see cref="ImageUiDownloads"/>。
    /// </summary>
    public int ImageNetworkAttempts { get; set; }

    /// <summary>
    /// UI 层（<c>ImageCache</c>）对敌人图鉴图源的真实下载成功次数（落盘且文件存在）。
    /// 补的是旧口径的盲区：<see cref="ImageFailures"/> 只覆盖解析诊断 + 解码失败，不含 ImageCache 下载失败。
    /// 与 <see cref="ImageUiDownloadFailures"/> 一起进 <see cref="ImageAttempts"/> 分母。
    /// </summary>
    public int ImageUiDownloads { get; set; }

    /// <summary>UI 层（<c>ImageCache</c>）对敌人图鉴图源的真实下载失败次数（catch 后仍无文件 → 降级占位图）。</summary>
    public int ImageUiDownloadFailures { get; set; }

    /// <summary>P6：命中随包原图的累计次数（零网络；列表页离线命中的主来源）。</summary>
    public int BundledImageHits { get; set; }

    /// <summary>P6：离线时用随包 256px 缩略图顶替原图的累计次数（详情大图降质预览）。</summary>
    public int BundledThumbPreviews { get; set; }

    /// <summary>
    /// 图片指标口径版本。
    /// P6 之前 <c>CachedImage</c> 把 file:// 送进下载管线并静默降级占位图，
    /// 于是「ImageFailures=0」是没被任何用户看到的 0，不能当基线。
    /// P6.1 起 <c>ImageFailures</c> 分子含 UI 层下载失败、<c>ImageAttempts</c> 分母含 UI 下载尝试，
    /// 与 p6 基线仍不可比（分子分母同时扩容），故再次升代次并清零重算。
    /// 缺这个字段（旧版本落盘的 JSON）= 旧基线，灰度 gate 不得用它判 W2-3。
    /// </summary>
    public string ImageMetricEpoch { get; set; } = string.Empty;

    /// <summary>口径迁移次数：&gt;0 表示这个安装曾把旧基线图片计数清零重算。</summary>
    public int ImageMetricResetCount { get; set; }

    public const string CurrentImageMetricEpoch = "p6.1-ui-download-20261109";
    public int SyncFailures { get; set; }
    public string? LastFallbackReason { get; set; }

    public void RecordSession(string now)
    {
        if (string.IsNullOrEmpty(FirstSessionAt))
        {
            FirstSessionAt = now;
        }

        LastSessionAt = now;
        Sessions++;
    }

    public void RecordWebLoad(double ms, int itemCount)
    {
        WebLoads++;
        LastItemCount = itemCount;
        LastLoadMs = Math.Round(ms, 3);
        MaxLoadMs = Math.Max(MaxLoadMs, LastLoadMs);
    }

    public void RecordDecision(WikiFirstRunDecision decision)
    {
        switch (decision.Mode)
        {
            case WikiFirstRunMode.LegacyFallback:
                WebFallbacks++;
                LastFallbackReason = decision.Reason;
                break;
            case WikiFirstRunMode.EmptyState:
                EmptyStates++;
                break;
        }
    }

    public void RecordImageFailures(int count)
    {
        if (count > 0)
        {
            ImageFailures += count;
        }
    }

    /// <summary>记录图片网络失败数（仅下载失败），网络口径分子；与解析口径分开累计，避免混算。</summary>
    public void RecordImageNetworkFailures(int count)
    {
        if (count > 0)
        {
            ImageNetworkFailures += count;
        }
    }

    /// <summary>记录图片解析尝试数（分母）。失败率 = ImageFailures / ImageAttempts。</summary>
    public void RecordImageAttempts(int count)
    {
        if (count > 0)
        {
            ImageAttempts += count;
        }
    }

    /// <summary>图片失败率（无尝试时返回 null，不写 0% 冒充达标）。分母=解析尝试 + UI 下载尝试。</summary>
    public double? ImageFailureRate => ImageAttempts > 0
        ? Math.Round(100.0 * ImageFailures / ImageAttempts, 2)
        : null;

    /// <summary>记录图片网络下载尝试数（= cache miss 数）；与解析尝试分开，供诊断纯网络失败率。</summary>
    /// <summary>UI 层真实下载的增量记账（成功 / 失败），由 <c>WikiImageDownloadCounter</c> 的进程内计数推导。</summary>
    public void RecordImageUiDownloads(int downloads, int failures)
    {
        if (downloads > 0)
        {
            ImageUiDownloads += downloads;
        }

        if (failures > 0)
        {
            ImageUiDownloadFailures += failures;
        }

        /* 分母同步扩容：每个 UI 下载尝试（成功或失败）都要在 ImageAttempts 里留下一条，
           否则 UI 失败会落进一个分母没覆盖的链路，比率被解析层大分母稀释。 */
        int attempts = Math.Max(0, downloads) + Math.Max(0, failures);
        if (attempts > 0)
        {
            ImageAttempts += attempts;
        }
    }

    /// <summary>UI 层真实下载尝试数（成功 + 失败）；0 表示这条链路本期没被触发，不是「没失败」。</summary>
    public int ImageUiDownloadAttempts => ImageUiDownloads + ImageUiDownloadFailures;

    /// <summary>
    /// UI 层下载失败率（小分母独立口径）。无尝试时返回 null —— 不许把「没下载过」写成 0%。
    /// 这条判据和 <see cref="ImageFailureRate"/> 分开：后者分母含十万级解析引用，
    /// 几十次 UI 失败在里面看不出来；这里只拿 UI 下载尝试做分母，失败 5/100 就是 5%。
    /// </summary>
    public double? ImageUiFailureRate => ImageUiDownloadAttempts > 0
        ? Math.Round(100.0 * ImageUiDownloadFailures / ImageUiDownloadAttempts, 2)
        : null;

    public void RecordImageNetworkAttempts(int count)
    {
        if (count > 0)
        {
            ImageNetworkAttempts += count;
        }
    }

    /// <summary>
    /// P6 随包口径：累计随包原图命中与离线缩略图预览次数。
    /// 这两个数不进失败率分母（它们不是网络尝试），只用于证明「离线时图片来自随包而非在线」。
    /// </summary>
    public void RecordBundledImageStats(int hits, int thumbPreviews)
    {
        if (hits > 0)
        {
            BundledImageHits += hits;
        }

        if (thumbPreviews > 0)
        {
            BundledThumbPreviews += thumbPreviews;
        }
    }

    /// <summary>
    /// 图片网络失败率 = 网络失败（仅下载失败）/ 真实下载尝试。
    /// 只作诊断：离线场景下天然接近 100%，不代表图片功能坏，**不参与 gate PASS 判定**。
    /// </summary>
    public double? ImageNetworkFailureRate => ImageNetworkAttempts > 0
        ? Math.Round(100.0 * ImageNetworkFailures / ImageNetworkAttempts, 2)
        : null;

    public void RecordSyncFailure()
    {
        SyncFailures++;
    }

    /// <summary>回退率 = 回退次数 / web 装配次数（无装配合成 0，不写 0% 冒充达标）。</summary>
    public double FallbackRate => WebLoads + WebFallbacks == 0
        ? 0
        : Math.Round(100.0 * WebFallbacks / (WebLoads + WebFallbacks), 2);

    public string ToSummaryText() =>
        $"灰度指标：会话 {Sessions} / web 装配 {WebLoads} / 回退 {WebFallbacks}（{FallbackRate}%）/ 空态 {EmptyStates} / "
        + $"图片失败 {ImageFailures}/{ImageAttempts}（{(ImageFailureRate is { } r ? r + "%" : "无尝试")}）/ "
        + $"UI 下载 {ImageUiDownloads} 成功 {ImageUiDownloadFailures} 失败（{(ImageUiFailureRate is { } u ? u + "%" : "无下载尝试")}）/ 同步失败 {SyncFailures}";

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>
/// 灰度期崩溃记录（W2-1）：只把「致命未处理异常」计入 <see cref="CrashCount"/>；
/// 可恢复的 UI 异常单独计入 <see cref="UiExceptionCount"/>，不混算成崩溃。
/// </summary>
public sealed class WikiCrashRecord
{
    public string Schema { get; init; } = "ellen-rollout-crash-v1";
    public int CrashCount { get; set; }
    public int UiExceptionCount { get; set; }
    public string? LastAt { get; set; }
    public string? LastSource { get; set; }
}

public static class WikiRolloutMetricsStore
{
    public const string FileName = "rollout-metrics.json";
    public const string CrashFileName = "rollout-crash.json";

    public static WikiRolloutMetrics Load(string root)
    {
        WikiRolloutMetrics metrics;
        try
        {
            string path = Path.Combine(root, FileName);
            metrics = File.Exists(path)
                ? JsonSerializer.Deserialize<WikiRolloutMetrics>(File.ReadAllText(path)) ?? new WikiRolloutMetrics()
                : new WikiRolloutMetrics();
        }
        catch (JsonException)
        {
            metrics = new WikiRolloutMetrics();
        }
        catch (IOException)
        {
            metrics = new WikiRolloutMetrics();
        }

        return MigrateImageEpoch(metrics);
    }

    /// <summary>
    /// 图片计数跨口径迁移：旧基线（P6 之前）的 ImageFailures/ImageAttempts 不可比，
    /// 一律清零重算，并在文件里留下 ImageMetricResetCount 供追溯。
    /// 只清图片相关计数，WebLoads/回退/崩溃等历史仍有效。
    /// </summary>
    private static WikiRolloutMetrics MigrateImageEpoch(WikiRolloutMetrics metrics)
    {
        if (string.Equals(metrics.ImageMetricEpoch, WikiRolloutMetrics.CurrentImageMetricEpoch, StringComparison.Ordinal))
        {
            return metrics;
        }

        // 全新文件（从未记录过图片）不需要记一次迁移，只打口径戳。
        bool hadLegacyImageData = metrics.ImageAttempts > 0 || metrics.ImageFailures > 0;
        metrics.ImageAttempts = 0;
        metrics.ImageFailures = 0;
        metrics.ImageNetworkAttempts = 0;
        metrics.ImageNetworkFailures = 0;
        metrics.ImageUiDownloads = 0;
        metrics.ImageUiDownloadFailures = 0;
        metrics.BundledImageHits = 0;
        metrics.BundledThumbPreviews = 0;
        metrics.ImageMetricEpoch = WikiRolloutMetrics.CurrentImageMetricEpoch;
        if (hadLegacyImageData)
        {
            metrics.ImageMetricResetCount++;
        }

        return metrics;
    }

    /// <summary>读-改-写；临时文件 + Move，进程被杀不会留下半个 JSON。</summary>
    public static void Save(string root, WikiRolloutMetrics metrics)
    {
        try
        {
            Directory.CreateDirectory(root);
            /* 落盘一律打口径戳：进程内新建的 metrics（还没经过 Load）也要带上，
               否则下次 Load 会把它当旧基线清零。从磁盘读出来的旧文件已在 Load 里迁移过，
               这里只是兜底，不会掩盖迁移（空戳且磁盘上有旧计数的情况已经在 Load 处理）。 */
            if (string.IsNullOrEmpty(metrics.ImageMetricEpoch))
            {
                metrics.ImageMetricEpoch = WikiRolloutMetrics.CurrentImageMetricEpoch;
            }

            string path = Path.Combine(root, FileName);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, metrics.ToJson());
            File.Move(temp, path, true);
        }
        catch (IOException)
        {
            // 指标落盘失败不影响主流程。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public static WikiCrashRecord LoadCrash(string root)
    {
        try
        {
            string path = Path.Combine(root, CrashFileName);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<WikiCrashRecord>(File.ReadAllText(path)) ?? new WikiCrashRecord()
                : new WikiCrashRecord();
        }
        catch (JsonException)
        {
            return new WikiCrashRecord();
        }
        catch (IOException)
        {
            return new WikiCrashRecord();
        }
    }

    /// <summary>
    /// 首次运行时创建 0 基线（若文件不存在）。这样「0 崩溃」与「未埋点」可区分：
    /// 文件存在且 CrashCount=0 = 已埋点且无崩溃；文件缺失 = 未埋点，W2-1 应判 MANUAL。
    /// </summary>
    public static void EnsureCrashRecord(string root)
    {
        try
        {
            string path = Path.Combine(root, CrashFileName);
            if (File.Exists(path))
            {
                return;
            }

            Directory.CreateDirectory(root);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new WikiCrashRecord(), new JsonSerializerOptions { WriteIndented = true }));
            using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// 崩溃计数落盘（best-effort）：崩溃路径上不允许再抛异常。
    /// <paramref name="fatal"/> = true 计入 CrashCount；false 计入 UiExceptionCount。
    /// </summary>
    public static void RecordCrash(string root, string source, bool fatal)
    {
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, CrashFileName);
            WikiCrashRecord record = LoadCrash(root);
            if (fatal)
            {
                record.CrashCount++;
            }
            else
            {
                record.UiExceptionCount++;
            }

            record.LastAt = DateTimeOffset.UtcNow.ToString("O");
            record.LastSource = source;
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));

            // 崩溃路径必须同步落盘：WriteAllText 只到 OS 缓冲，进程被杀可能丢计数。
            using (FileStream stream = new(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }
    }
}
