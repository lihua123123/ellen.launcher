using System.Collections.Generic;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P4 诊断语义：源降级与字段告警必须分开；C1/C2 统计不得混在一起。
/// </summary>
[TestClass]
public class WikiDiagnosticsSummaryTest
{
    private static WikiDiagnostic Diag(string kind, string detail) => new()
    {
        Kind = kind,
        Detail = detail,
        FallbackReason = kind,
        Source = WikiSourceStamp.EllenWiki,
        ManifestDigest = "digest",
    };

    [TestMethod]
    public void CurveAlertIsFieldWarningNotSourceDegradation()
    {
        WikiSyncState state = new()
        {
            Source = "ellen.wiki (http)",
            Digest = "d",
            ItemCount = 753,
            Diagnostics = [Diag("curve-strict-alert", "zip 内无 MonsterCurve，显式回退内置")],
        };

        WikiDiagnosticsSummary summary = state.Summary;
        Assert.IsFalse(summary.SourceDegraded, "曲线告警不是源降级");
        Assert.IsNull(summary.SourceFallbackReason);
        Assert.AreEqual(1, summary.FieldWarnings.Count);
        Assert.AreEqual(0, summary.SourceDegradations.Count);
        StringAssert.Contains(summary.SourceStateText, "源正常");
        StringAssert.Contains(summary.FieldWarningText, "curve-strict-alert");
    }

    [TestMethod]
    public void ItemFallbackIsSourceDegradationWithReason()
    {
        WikiSyncState state = new()
        {
            Source = "ellen.wiki (http)",
            ItemCount = 753,
            Diagnostics =
            [
                Diag("item-fallback", "abyss-x: HTTP 503；缓存命中"),
                Diag("curve-strict-alert", "curve"),
            ],
        };

        WikiDiagnosticsSummary summary = state.Summary;
        Assert.IsTrue(summary.SourceDegraded);
        StringAssert.Contains(summary.SourceFallbackReason!, "item-fallback");
        Assert.AreEqual(1, summary.SourceDegradations.Count);
        Assert.AreEqual(1, summary.FieldWarnings.Count, "同一份诊断里两类要各归各的");
        StringAssert.Contains(summary.SourceStateText, "源降级");
    }

    [TestMethod]
    public void C1AndC2StatsAreSeparated()
    {
        WikiSyncState state = new()
        {
            Source = "ellen.wiki (http)",
            Cache = new WikiCacheStats { Hits = 0, Misses = 754, Downloads = 754, NotModified = 0 },
            SnapshotCache = new WikiCacheStats { Hits = 1, Misses = 0, NotModified = 1, Downloads = 0 },
        };

        WikiDiagnosticsSummary summary = state.Summary;
        StringAssert.Contains(summary.C1CacheText, "C1 manifest");
        StringAssert.Contains(summary.C1CacheText, "304 1");
        StringAssert.Contains(summary.C2CacheText, "C2 enemies");
        StringAssert.Contains(summary.C2CacheText, "下载 754");
        Assert.AreNotEqual(summary.C1CacheText, summary.C2CacheText);
    }

    [TestMethod]
    public void ClassifierCoversAllDegradationKinds()
    {
        foreach (string kind in new[]
        {
            "snapshot-fallback", "index-fallback", "item-fallback", "http-failure",
            "image-download-failure", "background-sync-failure", "cache-index-recovered",
        })
        {
            Assert.IsTrue(WikiDiagnosticClassifier.IsSourceDegradation(kind), kind);
        }

        foreach (string kind in new[]
        {
            "curve-strict-alert", "cache-pruned", "cache-temp-swept", "enemy-count-mismatch",
            "digest-changed", "image-hash-mismatch", "alias-map-missing",
        })
        {
            Assert.IsFalse(WikiDiagnosticClassifier.IsSourceDegradation(kind), kind);
        }
    }
}
