// P1 只读数据层：溯源、诊断、开关与两套数据源契约。
// 本层不引用 WinUI / ViewModel / XAML，也不注册到运行时 DI；P2 才接入 UI。

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

/// <summary>每次读取都必须携带来源戳；回退必须带原因，禁止静默。</summary>
public sealed class WikiSourceStamp
{
    public const string EllenWiki = "ellen.wiki";
    public const string Bundled = "bundled";

    public string Source { get; init; } = "unknown";
    public string Contract { get; init; } = string.Empty;
    public string? Digest { get; init; }
    public string? GeneratedAt { get; init; }
    public string? FallbackReason { get; init; }

    public static WikiSourceStamp FromEllenWiki(string contract, string? digest, string? generatedAt) => new()
    {
        Source = EllenWiki,
        Contract = contract,
        Digest = digest,
        GeneratedAt = generatedAt,
    };

    public static WikiSourceStamp FromBundled(string reason) => new()
    {
        Source = Bundled,
        Contract = "legacy-722",
        FallbackReason = reason,
    };

    /// <summary>P5 客户端 753 条目 JSON 随包：离线首屏基线（方案 B 地基）。不是 722 回退，是预期的离线数据来源。</summary>
    public const string Bundled753 = "enemy-web-753-bundled";

    public static WikiSourceStamp FromBundledWeb753(string reason) => new()
    {
        Source = Bundled,
        Contract = Bundled753,
        FallbackReason = reason,
    };
}

public sealed class WikiDiagnostic
{
    public string Kind { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string? FallbackReason { get; init; }
    public string? ManifestDigest { get; init; }
    public string Detail { get; init; } = string.Empty;
}

/// <summary>诊断页消费的回退/告警日志；P1 由双读与单测写入。</summary>
public sealed class WikiDiagnosticsLog
{
    private readonly List<WikiDiagnostic> records = [];

    public IReadOnlyList<WikiDiagnostic> Records => records;

    public void Add(string kind, WikiSourceStamp stamp, string detail, string? fallbackReason = null)
    {
        records.Add(new WikiDiagnostic
        {
            Kind = kind,
            Source = stamp.Source,
            FallbackReason = fallbackReason ?? stamp.FallbackReason,
            ManifestDigest = stamp.Digest,
            Detail = detail,
        });
    }
}

/// <summary>P3 flag 持久化抽象：Domain 不引用主项目设置库，由主项目注入实现。</summary>
public interface IWikiFlagStore
{
    string? Read();
    void Write(string value);
}

public sealed class WikiSourceFeatureFlag
{
    public const string Legacy722Bundled = "legacy-722-bundled";
    public const string EnemyWeb753 = "enemy-web-753";

    private readonly IWikiFlagStore? store;

    public WikiSourceFeatureFlag(IWikiFlagStore? store = null)
    {
        this.store = store;
        // 重启保持：只接受两个已知值，未知值回退默认 legacy（不静默接受脏设置）。
        string? persisted = store?.Read();
        if (string.Equals(persisted, EnemyWeb753, StringComparison.Ordinal))
        {
            UiSource = EnemyWeb753;
        }

        StartupUiSource = UiSource;
    }

    public bool DoubleReadEnabled { get; set; } = true;

    /// <summary>P3 默认仍是旧 722 随包路径；切到 enemy-web-753 才走 ellen.wiki HTTP 源。</summary>
    public string UiSource { get; private set; } = Legacy722Bundled;

    /// <summary>
    /// 进程启动时、任何 <see cref="Switch"/> 之前的生效值。
    /// G7a 的「切换重启保持」必须看它：只看 <see cref="UiSource"/> 的话，
    /// 用户在设置里切一下、视图重新装配一次就满足了，根本没有重启。
    /// </summary>
    public string StartupUiSource { get; }

    /// <summary>
    /// 持久化设置里的值（只有用户手动 <see cref="Switch"/> 才写；灰度 <see cref="ApplyRollout"/> 不写）。
    /// G7a 的「切换重启保持」靠它区分「用户选的」和「灰度给的」。
    /// </summary>
    public string PersistedUiSource => store?.Read() ?? string.Empty;

    /// <summary>严格模式：任何非 ellen.wiki 来源都记告警，不静默回退。</summary>
    public bool Strict { get; set; }

    /// <summary>P5a：当前生效来源是灰度放量还是用户手动切换（诊断页展示，不参与业务判断）。</summary>
    public bool IsGrayscale { get; private set; }

    /// <summary>P5a：灰度决策原因（rollout=off / whitelist-hit / whitelist-miss / all / ui-source-override）。</summary>
    public string RolloutReason { get; private set; } = "rollout=off";

    /// <summary>P5a：安装 ID（哈希），白名单按它匹配；不落原始机器名。</summary>
    public string InstallId { get; private set; } = string.Empty;

    public bool IsWebMode => string.Equals(UiSource, EnemyWeb753, StringComparison.Ordinal);

    public bool IsLegacyMode => !IsWebMode;

    /// <summary>
    /// P5a 灰度决议落到 flag 上：只改内存中的生效值，**不写回**用户持久化设置
    /// （否则灰度结束后无法自动退回默认）。
    /// </summary>
    public void ApplyRollout(WikiRolloutDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        UiSource = string.Equals(decision.Source, EnemyWeb753, StringComparison.Ordinal) ? EnemyWeb753 : Legacy722Bundled;
        IsGrayscale = decision.IsGrayscale;
        RolloutReason = decision.Reason;
        InstallId = decision.InstallId;
    }

    /// <summary>切换 UI 数据源；只允许两个已知值，未知值拒绝切换（一键回滚即切回 Legacy722Bundled）。</summary>
    public bool Switch(string? source)
    {
        if (string.Equals(source, Legacy722Bundled, StringComparison.Ordinal))
        {
            UiSource = Legacy722Bundled;
            IsGrayscale = false;
            store?.Write(Legacy722Bundled);
            return true;
        }

        if (string.Equals(source, EnemyWeb753, StringComparison.Ordinal))
        {
            UiSource = EnemyWeb753;
            IsGrayscale = false;
            store?.Write(EnemyWeb753);
            return true;
        }

        return false;
    }

    /// <summary>P2 回滚演练：切到 web 再切回 legacy，返回是否回到默认状态。</summary>
    public bool RollbackDrill()
    {
        Switch(EnemyWeb753);
        Switch(Legacy722Bundled);
        return IsLegacyMode;
    }
}

public sealed class LauncherSnapshotReadResult
{
    public LauncherManifestDto Manifest { get; init; } = new();
    public IReadOnlyList<string> Files { get; init; } = [];
    public WikiSourceStamp Stamp { get; init; } = new();
    public IReadOnlyList<WikiDiagnostic> Diagnostics { get; init; } = [];
    public CurveValidation Curves { get; init; } = new();
}

public sealed class EnemyWebReadResult
{
    public EnemyIndexDocumentDto Index { get; init; } = new();
    public IReadOnlyDictionary<string, EnemyItemDto> Items { get; init; } = new Dictionary<string, EnemyItemDto>(StringComparer.Ordinal);
    public WikiSourceStamp Stamp { get; init; } = new();
    public IReadOnlyList<WikiDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>P4：manifest.datasets 的 enemies digest 未变且缓存完整 → 整段跳过网络请求。</summary>
    public bool DatasetCacheHit { get; init; }
}

/// <summary>C1：launcher-metadata.zip 契约（只读，不解压、不写本地）。</summary>
public interface ILauncherSnapshotSource
{
    ValueTask<LauncherSnapshotReadResult> ReadAsync(CancellationToken token = default);
}

/// <summary>C2：/api/enemies 契约（只读）。两套 DTO 严禁互相引用。</summary>
public interface IEnemyWebSource
{
    ValueTask<EnemyWebReadResult> ReadAsync(CancellationToken token = default);
}

/// <summary>可选：数据源暴露缓存统计，供诊断页展示命中率/304/下载量。File 源不实现。</summary>
public interface IWikiSourceStatsProvider
{
    WikiCacheStats Stats { get; }
}

/// <summary>
/// 可选：只读本地缓存、绝不触网的读取路径 —— 首屏用它，保证启动不被同步阻塞。
/// 缓存为空时返回 0 条，由上层显示「后台同步中」并保持 legacy。
/// </summary>
public interface ICachedEnemyWebSource
{
    ValueTask<EnemyWebReadResult> ReadCachedAsync(CancellationToken token = default);
}

/// <summary>同 <see cref="ICachedEnemyWebSource"/>，用于 C1 manifest 的纯缓存读取。</summary>
public interface ICachedLauncherSnapshotSource
{
    ValueTask<LauncherSnapshotReadResult> ReadCachedAsync(CancellationToken token = default);
}

/// <summary>
/// 诊断分类：把「数据源降级」和「字段级告警」分开。
/// 源降级 = 用户实际拿不到 ellen.wiki 数据（走了缓存/内置），必须显著提示；
/// 字段告警 = 数据本身可用，只是某个字段/能力缺失（曲线不在 zip、条目数不符、缓存超限淘汰）。
/// </summary>
public static class WikiDiagnosticClassifier
{
    private static readonly HashSet<string> DegradationKinds = new(StringComparer.Ordinal)
    {
        "snapshot-fallback",
        "index-fallback",
        "item-fallback",
        "http-failure",
        "image-download-failure",
        "background-sync-failure",
        "cache-index-recovered",
    };

    public static bool IsSourceDegradation(string kind) => DegradationKinds.Contains(kind);

    /// <summary>真正的数据降级诊断才映射为 fallbackReason；其余只作为字段告警展示。</summary>
    public static string? FirstDegradationReason(IReadOnlyList<WikiDiagnostic> diagnostics)
    {
        foreach (WikiDiagnostic d in diagnostics)
        {
            if (IsSourceDegradation(d.Kind))
            {
                return $"{d.Kind}: {d.Detail}";
            }
        }

        return null;
    }
}

/// <summary>诊断页载荷：源状态 + 字段告警 + C1/C2 分开的缓存统计。</summary>
public sealed class WikiDiagnosticsSummary
{
    public string Source { get; init; } = string.Empty;
    public bool SourceDegraded { get; init; }
    public string? SourceFallbackReason { get; init; }
    public IReadOnlyList<WikiDiagnostic> SourceDegradations { get; init; } = [];
    public IReadOnlyList<WikiDiagnostic> FieldWarnings { get; init; } = [];

    /// <summary>C1 = launcher-metadata.zip / manifest.json。</summary>
    public WikiCacheStats C1Cache { get; init; } = new();

    /// <summary>C2 = /api/enemies index + 753 items（图片下载也计在这里）。</summary>
    public WikiCacheStats C2Cache { get; init; } = new();

    public string SourceStateText => SourceDegraded
        ? $"源降级：{SourceFallbackReason ?? "未知原因"}"
        : "源正常：数据全部来自 ellen.wiki";

    public string FieldWarningText => FieldWarnings.Count == 0
        ? "无字段告警"
        : $"字段告警 {FieldWarnings.Count} 条：{string.Join("；", FieldWarnings.Select(w => w.Kind).Distinct())}";

    public string C1CacheText => $"C1 manifest：304 {C1Cache.NotModified} / 下载 {C1Cache.Downloads} / 失败 {C1Cache.Failures}";

    public string C2CacheText => $"C2 enemies：命中率 {C2Cache.HitRate}% / 304 {C2Cache.NotModified} / 下载 {C2Cache.Downloads} / 失败 {C2Cache.Failures}";
}

public sealed class CurveValidation
{
    public bool MonsterCurvePresentInZip { get; init; }
    public string? Alert { get; init; }

    /// <summary>DEF_1 在 launcher 侧是 GROW_CURVE_DEFENSE = Type 12，不是 index.curves 的字符串键。</summary>
    public const int DefenseCurveType = 12;

    public string Def1Rule => "MonsterCurve.json 必须保留 Type 12（GROW_CURVE_DEFENSE），P4 补进 zip";
}

public static class CurveContract
{
    public const string MonsterCurveFile = "MonsterCurve.json";

    public const string ValidateMissingReason = "launcher-metadata.zip 内无 MonsterCurve.json；严格模式告警，不静默回退内置快照";

    /// <summary>P4 决策：曲线权威源是 zip 内的 MonsterCurve；缺失只告警，绝不静默回退内置。</summary>
    public static CurveValidation Validate(LauncherManifestDto manifest)
    {
        bool present = manifest.Files is { } files && files.Any(f => string.Equals(f.Path, MonsterCurveFile, StringComparison.Ordinal));
        return new CurveValidation
        {
            MonsterCurvePresentInZip = present,
            Alert = present ? null : "严格模式告警：launcher-metadata.zip 内无 MonsterCurve.json；P1 不静默回退内置快照",
        };
    }
}
