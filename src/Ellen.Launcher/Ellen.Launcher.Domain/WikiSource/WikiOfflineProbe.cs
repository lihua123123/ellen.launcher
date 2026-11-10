using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ellen.Launcher.Domain.WikiSource;

/// <summary>
/// 一次「753 视图装配完成」的原始事实。只记观察结果，不记用户内容。
///
/// 为什么不是四个布尔：G7a 要证明的「随包 753 离线首启 / legacy 并存 / 回滚 / 切换重启保持」
/// 是**多次会话的行为组合**，单次启动判不出来。如果只落一个 `{xxxOk:true}`，
/// 那份证据和人工手写没有区别。这里保留 sessions 原文，gate 可以自己重算。
/// </summary>
public sealed class WikiSessionFact
{
    public string At { get; set; } = string.Empty;

    /// <summary>本次生效的 UI 源：<c>enemy-web-753</c> / <c>legacy-722-bundled</c>。</summary>
    public string UiSource { get; set; } = string.Empty;

    /// <summary>持久化设置里的值（只有用户手动 Switch 才会写；灰度放量不写）。</summary>
    public string PersistedUiSource { get; set; } = string.Empty;

    /// <summary>
    /// 进程启动时的生效值（任何 Switch 之前）。只有它等于 web-753，
    /// 才能证明「上一个进程写过持久化、这个进程读回来了」——即真的重启过。
    /// </summary>
    public string StartupUiSource { get; set; } = string.Empty;

    /// <summary>本次 753 是否由灰度放量带来（true = 非用户手动选择）。</summary>
    public bool IsGrayscale { get; set; }

    /// <summary>数据来自随包 753 离线基线（非 HTTP、非缓存）。</summary>
    public bool OfflineBundled { get; set; }

    /// <summary>本次装配出的条目数（753 / 722）。</summary>
    public int ItemCount { get; set; }

    /// <summary>累计回退次数快照（用于观察「这一轮有没有掉回 legacy」）。</summary>
    public int WebFallbacks { get; set; }

    /// <summary>
    /// 产生这条 session 的客户端版本（如 <c>7.2.0.1</c>）。
    /// 为什么要记：G7a 的结论只对「跑出这些 session 的那个构建」成立。
    /// 换包后如果不记版本，就分不清「这四条是旧包跑的」还是「新包又跑了一遍」，
    /// gate 里就会出现「G6/G7b 来自新包、G7a 来自旧包」的审计断点。
    /// 旧文件没有该字段 → 空串，gate 视为「版本未知」，不据此判 PASS。
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;
}

/// <summary>由 sessions 推导出的四项行为结论。gate 会优先重算，这里的值只作展示。</summary>
public sealed class WikiOfflineFacts
{
    /// <summary>存在一次：753 视图装配成功、数据来自随包、条目 ≥753。</summary>
    public bool OfflineBundled753Success { get; set; }

    /// <summary>753 与 722 两个视图都成功装配过（并存不冲突）。</summary>
    public bool LegacyCoexistOk { get; set; }

    /// <summary>发生过「web-753 → legacy-722」且回滚后装配出 ≥722 条。</summary>
    public bool RollbackOk { get; set; }

    /// <summary>用户手动切到 753 后，跨进程重启仍是 753（持久化设置生效、未被灰度覆盖）。</summary>
    public bool SwitchRestartKept { get; set; }
}

/// <summary>
/// G7a 行为探针（W1-5/6/8）：把「随包 753 离线命中、legacy 并存、回滚、切换重启保持」
/// 从**人工手写 JSON** 变成**客户端自证 + gate 可重算**。
///
/// 背景：`rollout-probe-offline.json` 原来没有任何生成器，文档写「测试手写」。
/// 手写的 true 等于自证，和 `pruneBoundaryOk` 之前的问题同一类。
/// </summary>
public sealed class WikiOfflineProbe
{
    public const string Schema = "wiki-offline-probe-v1";

    public string SchemaId { get; init; } = Schema;
    public string InstallId { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public List<WikiSessionFact> Sessions { get; set; } = [];
    public WikiOfflineFacts Facts { get; set; } = new();

    /// <summary>按时间顺序重算四项结论。sessions 为空时全部 false（缺数据 ≠ 通过）。</summary>
    public static WikiOfflineFacts Evaluate(IReadOnlyList<WikiSessionFact> sessions)
    {
        WikiOfflineFacts facts = new();
        if (sessions.Count == 0)
        {
            return facts;
        }

        const string web = WikiSourceFeatureFlag.EnemyWeb753;
        const string legacy = WikiSourceFeatureFlag.Legacy722Bundled;

        static bool IsWeb(WikiSessionFact s) => string.Equals(s.UiSource, web, StringComparison.Ordinal);
        static bool IsLegacy(WikiSessionFact s) => string.Equals(s.UiSource, legacy, StringComparison.Ordinal);

        bool anyWeb = sessions.Any(IsWeb);
        bool anyLegacy = sessions.Any(IsLegacy);

        facts.OfflineBundled753Success = sessions.Any(s => IsWeb(s) && s.OfflineBundled && s.ItemCount >= 753);
        facts.LegacyCoexistOk = anyWeb && anyLegacy;

        /* 回滚：必须在 web 之后出现过 legacy，且回滚后确实装配出了 722 条。
           只看「最后一条是 legacy」不够 —— 那可能本来就是 legacy 会话。 */
        int firstWeb = sessions.ToList().FindIndex(IsWeb);
        facts.RollbackOk = firstWeb >= 0
            && sessions.Skip(firstWeb + 1).Any(s => IsLegacy(s) && s.ItemCount >= 722);

        /* 重启保持：持久化值只由用户手动 Switch 写入（灰度 ApplyRollout 不写）。
           但只看 persisted=web 还不够 —— 用户在本进程里切一下，视图重新装配一次
           就会得到「persisted=web + uiSource=web + 非灰度」，根本没有重启。
           所以必须再加 startupUiSource=web：它只在**进程启动读回持久化**时才是 web，
           本进程内的 Switch 改不了它。 */
        facts.SwitchRestartKept = sessions.Any(s =>
            string.Equals(s.StartupUiSource, web, StringComparison.Ordinal)
            && IsWeb(s)
            && !s.IsGrayscale);

        return facts;
    }

    public void Recompute() => Facts = Evaluate(Sessions);

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

/// <summary>探针落盘：与 rollout-metrics.json 同目录；读-改-写 + 临时文件，进程被杀不留半个 JSON。</summary>
public static class WikiOfflineProbeStore
{
    public const string FileName = "rollout-probe-offline.json";

    /// <summary>只保留最近 N 次装配，避免长期灰度把文件撑大（N 次足够证明切换/回滚）。</summary>
    public const int MaxSessions = 100;

    public static WikiOfflineProbe Load(string root)
    {
        try
        {
            string path = Path.Combine(root, FileName);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<WikiOfflineProbe>(File.ReadAllText(path)) ?? new WikiOfflineProbe()
                : new WikiOfflineProbe();
        }
        catch (JsonException)
        {
            return new WikiOfflineProbe();
        }
        catch (IOException)
        {
            return new WikiOfflineProbe();
        }
    }

    public static void Save(string root, WikiOfflineProbe probe)
    {
        try
        {
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, FileName);
            string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, probe.ToJson());
            File.Move(temp, path, true);
        }
        catch (IOException)
        {
            // 探针落盘失败不影响主流程（gate 会按「缺数据」判 MANUAL，不会误判通过）。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>记一次装配事实并重算四项结论。返回写入后的探针，便于诊断页展示。</summary>
    public static WikiOfflineProbe Record(
        string root,
        string installId,
        string uiSource,
        string persistedUiSource,
        string startupUiSource,
        bool isGrayscale,
        bool offlineBundled,
        int itemCount,
        int webFallbacks,
        string appVersion = "")
    {
        WikiOfflineProbe probe = Load(root);
        probe.InstallId = installId;
        probe.Sessions.Add(new WikiSessionFact
        {
            At = DateTimeOffset.UtcNow.ToString("O"),
            UiSource = uiSource,
            PersistedUiSource = persistedUiSource,
            StartupUiSource = startupUiSource,
            IsGrayscale = isGrayscale,
            OfflineBundled = offlineBundled,
            ItemCount = itemCount,
            WebFallbacks = webFallbacks,
            AppVersion = appVersion,
        });

        if (probe.Sessions.Count > MaxSessions)
        {
            probe.Sessions.RemoveRange(0, probe.Sessions.Count - MaxSessions);
        }

        probe.UpdatedAt = DateTimeOffset.UtcNow.ToString("O");
        probe.Recompute();
        Save(root, probe);
        return probe;
    }
}
