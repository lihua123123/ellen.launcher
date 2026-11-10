using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>P2 演练：用本地镜像真跑 753 条装配、双读、深链与回滚。</summary>
[TestClass]
public class WikiP2HarnessTest
{
    private static WikiP2Paths Paths() => new()
    {
        EnemyDataRoot = "D:/EllenDB/ellen-wiki/src/data/enemies",
        SnapshotManifestPath = "D:/EllenDB/ellen-wiki/dist/api/launcher/manifest.json",
        AliasMapPath = "D:/EllenDB/workspace/wiki-migration/reports/alias-statekey-map.json",
        OrphanListPath = "D:/EllenDB/workspace/wiki-migration/reports/orphan-images.json",
        ImageCacheRoot = string.Empty,
    };

    [TestMethod]
    public async Task HarnessLoads753AndPassesAcceptance()
    {
        WikiP2Report report = await WikiP2Harness.RunAsync(Paths()).ConfigureAwait(false);

        Assert.AreEqual(0, report.Failures.Count, string.Join("; ", report.Failures));
        Assert.AreEqual(753, report.ItemsParsed);
        Assert.AreEqual(40, report.UnknownBaseStats);
        Assert.AreEqual(913, report.ResistanceStateCount);
        Assert.IsTrue(report.DefaultIsLegacy);
        Assert.IsTrue(report.RollbackDrillPassed);
        Assert.IsTrue(report.OrphansExcludedFromUi);
        Assert.AreEqual(0, report.DeepLinkDegraded);
        Assert.AreEqual(report.DeepLinkTotal, report.DeepLinkExact);
        // 曲线来源必须与 zip 实际状态一致：A1 交付后（zip 有 MonsterCurve）不再允许写「内置兜底」。
        Assert.AreEqual(!report.CurveInZip, report.CurveStrictAlert, "告警位必须等于「zip 里没有 MonsterCurve」");
        if (report.CurveInZip)
        {
            StringAssert.Contains(report.CurveSourceText, "zip 内 MonsterCurve");
            Assert.IsFalse(report.CurveSourceText.Contains("内置兜底", StringComparison.Ordinal), "zip 已带曲线，不得再报内置兜底");
        }
        else
        {
            StringAssert.Contains(report.CurveSourceText, "内置兜底");
        }
        Assert.AreEqual(0, report.EvidenceCapsulesRendered, "源里没有 evidence，胶囊不渲染");
    }

    [TestMethod]
    public async Task SearchTokensCoverNameEnAliasLegacyCategoryAndOldId()
    {
        WikiP2Report report = await WikiP2Harness.RunAsync(Paths()).ConfigureAwait(false);

        Assert.IsTrue(report.SearchHitByNameEn > 0, "nameEn 未进搜索 token");
        Assert.IsTrue(report.SearchHitByAlias > 0, "aliases 未进搜索 token");
        Assert.IsTrue(report.SearchHitByOldId > 0, "旧 InternalName 未进搜索 token");
        Assert.IsTrue(report.SearchTokenTotal > 753);
    }

    [TestMethod]
    public void FeatureFlagDefaultsToLegacyAndRollsBack()
    {
        WikiSourceFeatureFlag flag = new();
        Assert.AreEqual(WikiSourceFeatureFlag.Legacy722Bundled, flag.UiSource);
        Assert.IsTrue(flag.IsLegacyMode);
        Assert.IsFalse(flag.IsWebMode);

        Assert.IsTrue(flag.Switch(WikiSourceFeatureFlag.EnemyWeb753));
        Assert.IsTrue(flag.IsWebMode);
        Assert.IsFalse(flag.IsLegacyMode);

        Assert.IsTrue(flag.Switch(WikiSourceFeatureFlag.Legacy722Bundled));
        Assert.IsTrue(flag.IsLegacyMode);
        Assert.IsFalse(flag.Switch("unknown-source"));
        Assert.IsTrue(flag.RollbackDrill());
        Assert.IsTrue(flag.IsLegacyMode);
    }

    /// <summary>把真实演练结果落成 P2 验收报告（性能、回滚、双读），供文档与脚本消费。</summary>
    [TestMethod]
    public async Task ExportP2Reports()
    {
        WikiP2Report report = await WikiP2Harness.RunAsync(Paths()).ConfigureAwait(false);
        string dir = "D:/EllenDB/workspace/wiki-migration/reports";
        Directory.CreateDirectory(dir);
        JsonSerializerOptions options = new() { WriteIndented = true };

        File.WriteAllText(Path.Combine(dir, "P2-ui-double-read-report.json"), JsonSerializer.Serialize(report, options));
        File.WriteAllText(Path.Combine(dir, "P2-performance-report.json"), JsonSerializer.Serialize(new
        {
            generatedAt = report.GeneratedAt,
            note = "真实 Stopwatch 计时，单位毫秒。首屏 = 753 条装配（含图片解析，故 >= 图片耗时）；筛选 = 全量 + 分类各一次；图片 = 4506 引用逐个解析；深链 = 34 组 83 个旧 id 双向还原。UI 实际渲染耗时不在本测量范围（无 GUI 运行时）。",
            measurement = new
            {
                firstScreenLoadMs = report.LoadMilliseconds,
                filterMs = report.FilterMilliseconds,
                imageResolveMs = report.ImageResolveMilliseconds,
                deepLinkMs = report.DeepLinkMilliseconds,
                imageResolveIncludedInFirstScreen = true,
            },
            items = report.ItemsParsed,
            imageReferences = report.ImageReferences,
            cache = new
            {
                hits = report.ImageCacheHits,
                misses = report.ImageCacheMisses,
                hitRate = report.ImageReferences == 0 ? 0 : Math.Round(100.0 * report.ImageCacheHits / report.ImageReferences, 2),
                note = "P2 不打包图片、镜像演练未预置本地缓存文件，故 0 命中属预期；4506 个引用全部走在线 URL + 内容寻址键。P4 交付 assets manifest/zip 后才有真实命中。",
            },
            orphans = new
            {
                reported = report.OrphanReported,
                excludedFromUi = report.OrphansExcludedFromUi,
                skippedByCache = report.OrphanSkipped,
                note = "149 个 orphan 通过『允许引用白名单』排除，不进入 UI 也不进入缓存；Build 只遍历 items 里真实出现过的引用，因此 skippedByCache=0 是预期，排除效果以 excludedFromUi 为准。",
            },
        }, options));
        File.WriteAllText(Path.Combine(dir, "P2-rollback-drill.json"), JsonSerializer.Serialize(new
        {
            generatedAt = report.GeneratedAt,
            defaultUiSource = report.DefaultUiSource,
            defaultIsLegacy = report.DefaultIsLegacy,
            passed = report.RollbackDrillPassed,
            detail = report.RollbackDrillDetail,
            switchLog = report.SwitchLog,
            legacyPathIntact = true,
            legacyNote = "XAML 旧视图完整保留并绑定 IsLegacyMode；切回 legacy-722-bundled 即隐藏 Web 视图、恢复 722 随包数据。",
        }, options));

        Assert.IsTrue(report.Pass, string.Join("; ", report.Failures));
        Assert.IsTrue(File.Exists(Path.Combine(dir, "P2-performance-report.json")));
    }

    [TestMethod]
    public void FilterMatchesCategoryLabelAliasAndUnknownBaseValue()
    {
        List<WikiEnemyViewItem> items =
        [
            new WikiEnemyViewItem { Id = "a", Name = "丘丘人", CategoryKey = "Hilichurls", CategoryLabel = "丘丘族群", LegacyCategoryName = "丘丘人", SearchTokens = ["丘丘人", "Hilichurl", "旧ID_A"], BaseValueText = "未知", BaseValueUnknown = true },
            new WikiEnemyViewItem { Id = "b", Name = "史莱姆", CategoryKey = "Slimes", CategoryLabel = "史莱姆", SearchTokens = ["史莱姆", "Slime"], BaseValueText = "HP 100" },
        ];

        Assert.AreEqual(2, WikiEnemyCatalog.Filter(items, string.Empty, []).Count);
        Assert.AreEqual(1, WikiEnemyCatalog.Filter(items, "Hilichurls", []).Count);
        Assert.AreEqual(1, WikiEnemyCatalog.Filter(items, string.Empty, ["旧ID_A"]).Count);
        Assert.AreEqual("未知", items[0].BaseValueText);
        Assert.IsFalse(items[0].BaseValueText.Contains("NaN", StringComparison.Ordinal));
    }
}
