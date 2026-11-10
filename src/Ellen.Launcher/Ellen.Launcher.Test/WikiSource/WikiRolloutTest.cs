using System;
using System.Collections.Generic;
using System.IO;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P5 首次运行决策 + P5a 灰度白名单 + 指标落盘。纯逻辑，不联网。
/// </summary>
[TestClass]
public class WikiRolloutTest
{
    [TestMethod]
    public void NoCacheOfflineFallsBackToLegacyWithVisibleBanner()
    {
        WikiFirstRunDecision decision = WikiFirstRunPolicy.Decide(new WikiFirstRunInputs
        {
            WebEnabled = true,
            CachedItemCount = 0,
            Online = false,
            FailureReason = "Connection refused",
        });

        Assert.AreEqual(WikiFirstRunMode.LegacyFallback, decision.Mode);
        Assert.IsTrue(decision.IsFallback);
        Assert.AreEqual("no-cache-offline", decision.Reason);
        StringAssert.Contains(decision.Banner, "内置 722");
        StringAssert.Contains(decision.Banner, "Connection refused");
    }

    [TestMethod]
    public void NoCacheOnlineShowsEmptyStateNotBlank()
    {
        WikiFirstRunDecision decision = WikiFirstRunPolicy.Decide(new WikiFirstRunInputs
        {
            WebEnabled = true,
            CachedItemCount = 0,
            Online = true,
        });

        Assert.AreEqual(WikiFirstRunMode.EmptyState, decision.Mode);
        Assert.IsFalse(decision.IsFallback);
        Assert.IsNotNull(decision.Banner);
    }

    [TestMethod]
    public void CacheHitWorksOfflineAndSaysSo()
    {
        WikiFirstRunDecision decision = WikiFirstRunPolicy.Decide(new WikiFirstRunInputs
        {
            WebEnabled = true,
            CachedItemCount = 753,
            Online = false,
        });

        Assert.AreEqual(WikiFirstRunMode.Web, decision.Mode);
        Assert.AreEqual("cache-hit", decision.Reason);
        StringAssert.Contains(decision.Banner, "753");
    }

    [TestMethod]
    public void DisabledWebNeverFallsBack()
    {
        WikiFirstRunDecision decision = WikiFirstRunPolicy.Decide(new WikiFirstRunInputs { WebEnabled = false });
        Assert.AreEqual(WikiFirstRunMode.Web, decision.Mode);
        Assert.IsNull(decision.Banner);
    }

    [TestMethod]
    public void RolloutOffHonoursPersistedFlag()
    {
        WikiRolloutDecision web = WikiRolloutPolicy.Resolve("enemy-web-753", WikiRolloutMode.Off, "id1", []);
        Assert.IsTrue(web.IsWeb);
        Assert.AreEqual("rollout=off", web.Reason);

        WikiRolloutDecision legacy = WikiRolloutPolicy.Resolve("legacy-722-bundled", WikiRolloutMode.Off, "id1", []);
        Assert.IsFalse(legacy.IsWeb);
    }

    [TestMethod]
    public void WhitelistHitEnablesGrayscaleAndMissKeepsLegacy()
    {
        WikiRolloutDecision hit = WikiRolloutPolicy.Resolve("legacy-722-bundled", WikiRolloutMode.Whitelist, "abc123", ["abc123"]);
        Assert.IsTrue(hit.IsWeb);
        Assert.IsTrue(hit.IsGrayscale);
        Assert.AreEqual("rollout=whitelist-hit", hit.Reason);

        WikiRolloutDecision miss = WikiRolloutPolicy.Resolve("enemy-web-753", WikiRolloutMode.Whitelist, "abc123", ["other"]);
        Assert.IsFalse(miss.IsWeb);
        Assert.IsFalse(miss.IsGrayscale);
        Assert.AreEqual("rollout=whitelist-miss", miss.Reason);
    }

    [TestMethod]
    public void RolloutAllEnablesEveryone()
    {
        WikiRolloutDecision decision = WikiRolloutPolicy.Resolve("legacy-722-bundled", WikiRolloutMode.All, "id", []);
        Assert.IsTrue(decision.IsWeb);
        Assert.AreEqual("rollout=all", decision.Reason);
    }

    [TestMethod]
    public void UiSourceOverrideOnlyAcceptsKnownValues()
    {
        Assert.IsTrue(WikiRolloutPolicy.Resolve(null, WikiRolloutMode.Off, "id", [], "enemy-web-753").IsWeb);
        Assert.IsFalse(WikiRolloutPolicy.Resolve("legacy-722-bundled", WikiRolloutMode.Off, "id", [], "garbage").IsWeb);
    }

    [TestMethod]
    public void ApplyRolloutDoesNotWritePersistedSetting()
    {
        InMemoryFlagStore store = new() { Value = "legacy-722-bundled" };
        WikiSourceFeatureFlag flag = new(store);
        flag.ApplyRollout(new WikiRolloutDecision { Source = WikiSourceFeatureFlag.EnemyWeb753, IsGrayscale = true, Reason = "rollout=whitelist-hit", InstallId = "abc" });

        Assert.IsTrue(flag.IsWebMode);
        Assert.IsTrue(flag.IsGrayscale);
        Assert.AreEqual("legacy-722-bundled", store.Value, "灰度决议不得写回用户设置");
    }

    [TestMethod]
    public void UiImageDownloadsAreCountedSeparatelyFromResolverMisses()
    {
        WikiRolloutMetrics metrics = new();
        metrics.RecordImageNetworkAttempts(759);   // 解析层未命中：不是下载次数
        metrics.RecordImageUiDownloads(3, 0);
        metrics.RecordImageUiDownloads(0, 2);

        Assert.AreEqual(759, metrics.ImageNetworkAttempts, "解析层未命中口径不变");
        Assert.AreEqual(3, metrics.ImageUiDownloads, "真实下载成功数与未命中数分开记账");
        Assert.AreEqual(2, metrics.ImageUiDownloadFailures, "下载失败单独累计，便于与占位图现象对照");
        Assert.AreEqual(5, metrics.ImageUiDownloadAttempts, "UI 下载尝试 = 成功 + 失败");
        Assert.AreEqual(5, metrics.ImageAttempts, "UI 下载尝试必须进失败率分母，否则分子来自分母未覆盖的链路");
    }

    [TestMethod]
    public void UiDownloadFailureRateUsesItsOwnSmallDenominator()
    {
        /* 用户指的盲区：解析层分母十万级时，UI 下载 100 次失败 5 次会被稀释成 0.03%，
           主判据 <1% 照样 PASS，但真实 UI 失败率是 5%。所以必须有一条独立的小分母判据。 */
        WikiRolloutMetrics metrics = new();
        metrics.RecordImageAttempts(18024);        // 解析层引用（大分母）
        metrics.RecordImageUiDownloads(95, 5);     // UI 下载 100 次，失败 5 次
        metrics.RecordImageFailures(5);            // 失败同时进主分子

        Assert.AreEqual(18124, metrics.ImageAttempts, "分母 = 解析尝试 + UI 下载尝试（18024 + 100）");
        Assert.IsTrue(metrics.ImageFailureRate < 0.1, $"主口径被大分母稀释：{metrics.ImageFailureRate}%");
        Assert.AreEqual(5.0, metrics.ImageUiFailureRate, "独立口径 5/100 = 5%，不能被稀释");
        Assert.AreEqual(100, metrics.ImageUiDownloadAttempts);
    }

    [TestMethod]
    public void UiDownloadRateIsNullWhenNothingWasDownloaded()
    {
        // 「没下载过」≠「没失败」：返回 null，让 gate 判「无样本」，不许写成 0%。
        WikiRolloutMetrics metrics = new();
        metrics.RecordImageAttempts(4506);

        Assert.IsNull(metrics.ImageUiFailureRate);
        Assert.AreEqual(0, metrics.ImageUiDownloadAttempts);
    }

    [TestMethod]
    public void MetricsPersistAndComputeFallbackRate()
    {
        string root = Path.Combine(Path.GetTempPath(), "EllenRolloutTest", Guid.NewGuid().ToString("N"));
        WikiRolloutMetrics metrics = new();
        metrics.RecordSession("2026-11-08T00:00:00Z");
        metrics.RecordWebLoad(12.5, 753);
        metrics.RecordDecision(new WikiFirstRunDecision { Mode = WikiFirstRunMode.LegacyFallback, Reason = "no-cache-offline" });
        metrics.RecordImageFailures(2);
        metrics.RecordImageNetworkFailures(1);
        metrics.RecordImageAttempts(400);
        metrics.RecordImageNetworkAttempts(40);
        metrics.RecordSyncFailure();
        WikiRolloutMetricsStore.Save(root, metrics);

        WikiRolloutMetrics loaded = WikiRolloutMetricsStore.Load(root);
        Assert.AreEqual(1, loaded.Sessions);
        Assert.AreEqual(1, loaded.WebLoads);
        Assert.AreEqual(1, loaded.WebFallbacks);
        Assert.AreEqual(50.0, loaded.FallbackRate);
        Assert.AreEqual(2, loaded.ImageFailures);
        Assert.AreEqual(400, loaded.ImageAttempts, "图片失败率分母必须持久化");
        Assert.AreEqual(0.5, loaded.ImageFailureRate, "2/400 = 0.5%（解析口径）");
        Assert.AreEqual(40, loaded.ImageNetworkAttempts, "网络下载尝试分母必须持久化（= cache miss）");
        Assert.AreEqual(1, loaded.ImageNetworkFailures, "网络分子只计下载失败");
        Assert.AreEqual(2.5, loaded.ImageNetworkFailureRate, "1/40 = 2.5%（网络口径，仅诊断）");
        Assert.AreEqual(1, loaded.SyncFailures);
        StringAssert.Contains(loaded.ToSummaryText(), "回退 1");
    }

    [TestMethod]
    public void LegacyImageBaselineIsResetInsteadOfCompared()
    {
        // P6 之前 CachedImage 不渲染 file:// 本地图，ImageFailures 恒为 0 = 「没测量」。
        // 旧文件没有 ImageMetricEpoch，Load 必须清零图片计数，否则 W2-3 会拿假的 0% 判通过。
        string root = Path.Combine(Path.GetTempPath(), "EllenImageEpoch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, WikiRolloutMetricsStore.FileName), """
            {
              "Schema": "wiki-rollout-metrics-v1",
              "InstallId": "legacy-install",
              "WebLoads": 7,
              "WebFallbacks": 1,
              "ImageAttempts": 4506,
              "ImageFailures": 0,
              "BundledImageHits": 3747
            }
            """);

        WikiRolloutMetrics loaded = WikiRolloutMetricsStore.Load(root);

        Assert.AreEqual(0, loaded.ImageAttempts, "旧基线图片分母必须清零（不可比）");
        Assert.AreEqual(0, loaded.ImageFailures, "旧基线图片分子必须清零");
        Assert.AreEqual(0, loaded.BundledImageHits, "随包命中计数同样属于图片口径");
        Assert.AreEqual(WikiRolloutMetrics.CurrentImageMetricEpoch, loaded.ImageMetricEpoch);
        Assert.AreEqual(1, loaded.ImageMetricResetCount, "迁移要留痕，便于事后追溯");
        Assert.AreEqual(7, loaded.WebLoads, "WebLoads 等非图片历史仍有效，不能被一并清掉");
        Assert.AreEqual(1, loaded.WebFallbacks);
    }

    [TestMethod]
    public void CrashRecordBaselineIsZeroAndDistinguishableFromMissing()
    {
        string root = Path.Combine(Path.GetTempPath(), "EllenCrashBaseline", Guid.NewGuid().ToString("N"));
        WikiRolloutMetricsStore.EnsureCrashRecord(root);

        Assert.IsTrue(File.Exists(Path.Combine(root, "rollout-crash.json")), "首次运行必须落 0 基线");
        Assert.AreEqual(0, WikiRolloutMetricsStore.LoadCrash(root).CrashCount);
    }

    [TestMethod]
    public void CrashRecordPersistsFatalAndUiSeparately()
    {
        string root = Path.Combine(Path.GetTempPath(), "EllenCrashTest", Guid.NewGuid().ToString("N"));
        WikiRolloutMetricsStore.RecordCrash(root, "AppDomain.UnhandledException", fatal: true);
        WikiRolloutMetricsStore.RecordCrash(root, "Application.UnhandledException", fatal: false);

        WikiCrashRecord loaded = WikiRolloutMetricsStore.LoadCrash(root);
        Assert.AreEqual(1, loaded.CrashCount, "致命未处理异常计入 CrashCount");
        Assert.AreEqual(1, loaded.UiExceptionCount, "可恢复 UI 异常单独计数，不混算崩溃");
        Assert.AreEqual("Application.UnhandledException", loaded.LastSource);
    }

    private sealed class InMemoryFlagStore : IWikiFlagStore
    {
        public string? Value { get; set; }

        public string? Read() => Value;

        public void Write(string value) => Value = value;
    }
}
