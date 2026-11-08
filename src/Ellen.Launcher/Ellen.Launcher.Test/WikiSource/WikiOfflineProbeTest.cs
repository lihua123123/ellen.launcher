using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// G7a 行为探针的推导规则：四项结论必须能从 sessions 原文重算，
/// 且「缺数据」一律 false（不能默认 true）。
/// </summary>
[TestClass]
public class WikiOfflineProbeTest
{
    private const string Web = WikiSourceFeatureFlag.EnemyWeb753;
    private const string Legacy = WikiSourceFeatureFlag.Legacy722Bundled;

    private static WikiSessionFact Session(
        string uiSource,
        string persisted = "",
        string startup = "",
        bool grayscale = false,
        bool offlineBundled = false,
        int itemCount = 0)
    {
        return new WikiSessionFact
        {
            At = "2026-11-08T00:00:00+00:00",
            UiSource = uiSource,
            PersistedUiSource = persisted,
            StartupUiSource = startup,
            IsGrayscale = grayscale,
            OfflineBundled = offlineBundled,
            ItemCount = itemCount,
        };
    }

    [TestMethod]
    public void Evaluate_EmptySessions_AllFalse()
    {
        WikiOfflineFacts facts = WikiOfflineProbe.Evaluate([]);

        Assert.IsFalse(facts.OfflineBundled753Success);
        Assert.IsFalse(facts.LegacyCoexistOk);
        Assert.IsFalse(facts.RollbackOk);
        Assert.IsFalse(facts.SwitchRestartKept);
    }

    [TestMethod]
    public void Evaluate_Bundled753_RequiresOfflineAnd753()
    {
        WikiSessionFact hit = Session(Web, offlineBundled: true, itemCount: 753);
        Assert.IsTrue(WikiOfflineProbe.Evaluate([hit]).OfflineBundled753Success);

        // 走 HTTP 缓存（offlineBundled=false）不算「随包离线命中」
        WikiSessionFact http = Session(Web, offlineBundled: false, itemCount: 753);
        Assert.IsFalse(WikiOfflineProbe.Evaluate([http]).OfflineBundled753Success);

        // 条目不足 753 也不算
        WikiSessionFact shortList = Session(Web, offlineBundled: true, itemCount: 700);
        Assert.IsFalse(WikiOfflineProbe.Evaluate([shortList]).OfflineBundled753Success);
    }

    [TestMethod]
    public void Evaluate_LegacyCoexist_NeedsBothViews()
    {
        Assert.IsFalse(WikiOfflineProbe.Evaluate([Session(Web, offlineBundled: true, itemCount: 753)]).LegacyCoexistOk);

        bool both = WikiOfflineProbe.Evaluate([
            Session(Web, offlineBundled: true, itemCount: 753),
            Session(Legacy, itemCount: 722),
        ]).LegacyCoexistOk;

        Assert.IsTrue(both);
    }

    [TestMethod]
    public void Evaluate_Rollback_RequiresLegacyAfterWebWith722()
    {
        // 只有 legacy：本来就默认 722，不算回滚
        Assert.IsFalse(WikiOfflineProbe.Evaluate([Session(Legacy, itemCount: 722)]).RollbackOk);

        // web 之后接 legacy 且装配出 722 条 → 回滚成立
        bool rolledBack = WikiOfflineProbe.Evaluate([
            Session(Web, offlineBundled: true, itemCount: 753),
            Session(Legacy, itemCount: 722),
        ]).RollbackOk;
        Assert.IsTrue(rolledBack);

        // web 之后回滚但条目不足（视图没真起来）→ 不算
        bool broken = WikiOfflineProbe.Evaluate([
            Session(Web, offlineBundled: true, itemCount: 753),
            Session(Legacy, itemCount: 0),
        ]).RollbackOk;
        Assert.IsFalse(broken);
    }

    [TestMethod]
    public void Evaluate_SwitchRestartKept_RequiresStartupValue()
    {
        // 上一个进程写过持久化 → 本进程启动即 web，且非灰度：重启保持成立
        bool kept = WikiOfflineProbe.Evaluate([
            Session(Web, persisted: Web, startup: Web, grayscale: false, offlineBundled: true, itemCount: 753),
        ]).SwitchRestartKept;
        Assert.IsTrue(kept);

        // 灰度放量：persisted 空、startup 仍是 legacy → 不能证明「重启保持用户设置」
        bool grayscale = WikiOfflineProbe.Evaluate([
            Session(Web, persisted: string.Empty, startup: Legacy, grayscale: true, offlineBundled: true, itemCount: 753),
        ]).SwitchRestartKept;
        Assert.IsFalse(grayscale);
    }

    [TestMethod]
    public void Evaluate_SwitchRestartKept_SameProcessSwitchDoesNotCount()
    {
        /* 关键反例：用户在设置里切到 753（persisted 立刻变 web），视图重新装配一次，
           整个过程没有重启 —— startup 仍是 legacy，不能算「重启保持」。
           旧判据（只看 persisted）在这里会误判 true。 */
        bool sameProcess = WikiOfflineProbe.Evaluate([
            Session(Legacy, persisted: string.Empty, startup: Legacy, itemCount: 722),
            Session(Web, persisted: Web, startup: Legacy, offlineBundled: true, itemCount: 753),
        ]).SwitchRestartKept;

        Assert.IsFalse(sameProcess);
    }

    [TestMethod]
    public void Store_Record_AccumulatesSessionsAndRecomputes()
    {
        string root = Path.Combine(Path.GetTempPath(), "ellen-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            WikiOfflineProbeStore.Record(root, "install-1", Web, Web, Web, isGrayscale: false, offlineBundled: true, itemCount: 753, webFallbacks: 0, appVersion: "7.2.0.0");
            WikiOfflineProbe probe = WikiOfflineProbeStore.Record(root, "install-1", Legacy, Web, Web, isGrayscale: false, offlineBundled: false, itemCount: 722, webFallbacks: 0, appVersion: "7.2.0.1");

            Assert.AreEqual(2, probe.Sessions.Count);
            Assert.AreEqual("7.2.0.0", probe.Sessions[0].AppVersion, "每条 session 要记产生它的构建版本");
            Assert.AreEqual("7.2.0.1", probe.Sessions[1].AppVersion, "跨版本重跑后能看出证据属于哪个包");
            Assert.IsTrue(probe.Facts.OfflineBundled753Success);
            Assert.IsTrue(probe.Facts.LegacyCoexistOk);
            Assert.IsTrue(probe.Facts.RollbackOk);
            Assert.IsTrue(probe.Facts.SwitchRestartKept);
            Assert.IsTrue(File.Exists(Path.Combine(root, WikiOfflineProbeStore.FileName)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [TestMethod]
    public void Store_Sessions_AreCapped()
    {
        string root = Path.Combine(Path.GetTempPath(), "ellen-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            WikiOfflineProbe probe = new();
            for (int i = 0; i < WikiOfflineProbeStore.MaxSessions + 10; i++)
            {
                probe = WikiOfflineProbeStore.Record(root, "install-1", Legacy, string.Empty, Legacy, false, false, 722, 0);
            }

            Assert.AreEqual(WikiOfflineProbeStore.MaxSessions, probe.Sessions.Count);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    /// <summary>
    /// 跨实现一致性：gate 的 <c>evaluateOfflineSessions</c> 与本类 <see cref="WikiOfflineProbe.Evaluate"/>
    /// 是同一套规则的两个实现。共用 <c>tools/offline-probe-vectors.json</c>，
    /// 任一实现漂移都会在 C# 或 gate 侧测试里失败。
    /// </summary>
    [TestMethod]
    public void Evaluate_SharedVectors_MatchExpected()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "offline-probe-vectors.json");
        Assert.IsTrue(File.Exists(path), $"共享判定向量缺失：{path}");

        ProbeVectorsDocument doc = JsonSerializer.Deserialize<ProbeVectorsDocument>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.IsTrue(doc.Cases.Count >= 6, "向量数量过少，覆盖不足");

        foreach (ProbeVector vector in doc.Cases)
        {
            WikiOfflineFacts facts = WikiOfflineProbe.Evaluate(vector.Sessions);
            Assert.AreEqual(vector.Expected.OfflineBundled753Success, facts.OfflineBundled753Success, $"{vector.Id}: offlineBundled753Success");
            Assert.AreEqual(vector.Expected.LegacyCoexistOk, facts.LegacyCoexistOk, $"{vector.Id}: legacyCoexistOk");
            Assert.AreEqual(vector.Expected.RollbackOk, facts.RollbackOk, $"{vector.Id}: rollbackOk");
            Assert.AreEqual(vector.Expected.SwitchRestartKept, facts.SwitchRestartKept, $"{vector.Id}: switchRestartKept");
        }
    }

    private sealed class ProbeVectorsDocument
    {
        public List<ProbeVector> Cases { get; set; } = [];
    }

    private sealed class ProbeVector
    {
        public string Id { get; set; } = string.Empty;
        public List<WikiSessionFact> Sessions { get; set; } = [];
        public WikiOfflineFacts Expected { get; set; } = new();
    }
}
