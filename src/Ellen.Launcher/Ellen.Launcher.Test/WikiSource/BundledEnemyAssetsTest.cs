using System;
using System.IO;
using System.Linq;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P6 随包图片（方案 B 最后一层）：清单 + 原图 + 256px 缩略图能否被客户端直接解析。
/// 断言用真实随包目录（Installer/Publish/Resource/EnemyArchives/Web753/assets），
/// 目录不存在时 Inconclusive —— 不伪装通过。
/// </summary>
[TestClass]
public class BundledEnemyAssetsTest
{
    private const string BundleAssets = "D:/EllenDB/ellen-launcher/Installer/Publish/Resource/EnemyArchives/Web753/assets";

    private static EnemyAssetsBundle LoadOrSkip(WikiDiagnosticsLog log)
    {
        if (!Directory.Exists(BundleAssets))
        {
            Assert.Inconclusive("随包图片未生成（python tools/EllenWikiSync/sync_assets.py）");
        }

        return EnemyAssetsBundle.Load(BundleAssets, log);
    }

    [TestMethod]
    public void BundleLoadsManifestLicenseAndCounts()
    {
        WikiDiagnosticsLog log = new();
        EnemyAssetsBundle bundle = LoadOrSkip(log);

        Assert.IsTrue(bundle.Present, "随包清单必须可解析");
        Assert.AreEqual(WikiAssetManifest.ExpectedSchema, bundle.Manifest!.Dto.Schema);
        Assert.AreEqual(4506, bundle.Manifest.Count, "清单条数 = 753 条 item 的全部引用");
        Assert.IsTrue(bundle.OriginalCount > 3000, $"随包原图数异常：{bundle.OriginalCount}");
        Assert.AreEqual(bundle.Manifest.DeclaredThumbCount, bundle.ThumbCount, "缩略图声明数必须等于落盘数");
        Assert.IsTrue(bundle.ThumbCount > 0, "随包缩略图为空");
        Assert.IsTrue(bundle.Bytes > 20 * 1024 * 1024 && bundle.Bytes < 40 * 1024 * 1024,
            $"随包体积应在 20–40 MiB：{bundle.Bytes / 1024.0 / 1024.0:F1} MiB");

        // 许可与署名（P5-offline-package §3 验收 #5 / #7）
        Assert.IsTrue(bundle.Redistributable, "随包必须基于 redistributable=true 的清单");
        Assert.IsFalse(string.IsNullOrWhiteSpace(bundle.Attribution), "license.attribution 不能为空（验收 #7）");
        StringAssert.Contains(bundle.Attribution!, "miHoYo");
        Assert.AreEqual("ellen", bundle.License!.ApprovedBy);
        Assert.IsTrue(log.Records.Any(d => d.Kind == "assets-bundle-thumb-drift") == false,
            "清单声明数与落盘数不一致会记 assets-bundle-thumb-drift");
    }

    [TestMethod]
    public void SmallIconsResolveToBundledOriginalWithoutNetwork()
    {
        WikiDiagnosticsLog log = new();
        EnemyAssetsBundle bundle = LoadOrSkip(log);

        // avatar.webp 没有 thumb → 随包的就是原图（全质量）
        EnemyAssetFileDto avatar = bundle.Manifest!.Dto.Files.First(f => f.File.EndsWith("avatar.webp", StringComparison.Ordinal));
        Assert.IsTrue(bundle.TryResolveOriginal(avatar.Id, avatar.File, out string path), $"{avatar.Key} 应随包");
        Assert.IsTrue(File.Exists(path));

        WikiImageCache images = new(
            bundle.Manifest.Keys,
            cacheRoot: string.Empty,
            log,
            WikiSourceStamp.FromBundledWeb753("test"),
            localResolver: null,
            bundle,
            preferBundledThumb: true);

        string uri = images.Resolve(new WikiImageRef
        {
            EnemyId = avatar.Id,
            File = avatar.File,
            CacheKey = avatar.Key,
            Uri = "https://ellen.wiki/images/enemies/" + avatar.Key,
        });

        Assert.IsFalse(uri.StartsWith("http", StringComparison.OrdinalIgnoreCase), $"随包命中时不得走网络：{uri}");
        Assert.AreEqual(1, images.BundledHits);
        Assert.AreEqual(0, images.Misses);
    }

    [TestMethod]
    public void DetailArtUsesThumbOnlyWhenOffline()
    {
        WikiDiagnosticsLog log = new();
        EnemyAssetsBundle bundle = LoadOrSkip(log);
        EnemyAssetFileDto art = bundle.Manifest!.Dto.Files.First(f => f.Thumb is not null);

        WikiImageRef Ref() => new()
        {
            EnemyId = art.Id,
            File = art.File,
            CacheKey = art.Key,
            Uri = "https://ellen.wiki/images/enemies/" + art.Key,
        };

        // 在线：大图必须走原图 URL，不能被 256px 缩略图顶掉画质
        WikiImageCache online = new(bundle.Manifest.Keys, string.Empty, log,
            WikiSourceStamp.FromEllenWiki("test", null, null), null, bundle, preferBundledThumb: false);
        string onlineUri = online.Resolve(Ref());
        Assert.IsTrue(onlineUri.StartsWith("https://ellen.wiki/", StringComparison.Ordinal), onlineUri);
        Assert.AreEqual(0, online.BundledThumbPreviews);

        // 离线：没有本地原图时用随包缩略图顶上，而不是掉占位图
        WikiImageCache offline = new(bundle.Manifest.Keys, string.Empty, log,
            WikiSourceStamp.FromBundledWeb753("test"), null, bundle, preferBundledThumb: true);
        string offlineUri = offline.Resolve(Ref());
        Assert.IsFalse(offlineUri.StartsWith("http", StringComparison.Ordinal), offlineUri);
        Assert.AreNotEqual(WikiImageCache.PlaceholderUri, offlineUri, "离线大图不应直接掉占位图");
        Assert.AreEqual(1, offline.BundledThumbPreviews);
        Assert.IsTrue(File.Exists(new Uri(offlineUri).LocalPath), offlineUri);
    }

    [TestMethod]
    public void MissingBundleDegradesExplicitlyWithoutThrowing()
    {
        WikiDiagnosticsLog log = new();
        EnemyAssetsBundle bundle = EnemyAssetsBundle.Load(Path.Combine(Path.GetTempPath(), "EllenNoAssets", Guid.NewGuid().ToString("N")), log);

        Assert.IsFalse(bundle.Present);
        Assert.AreEqual(0, bundle.ThumbCount);
        Assert.AreEqual("随包图片：未打包（走缓存/在线）", bundle.SummaryText);

        bool hasDiagnostic = log.Records.Any(d => d.Kind == "assets-bundle-missing");
        Assert.IsTrue(hasDiagnostic, "缺随包目录必须显式记诊断，不能静默");

        // 空包不参与解析：仍按原逻辑回落在在线 URL
        WikiImageCache images = new(["x/y.webp"], string.Empty, log,
            WikiSourceStamp.FromEllenWiki("test", null, null), null, EnemyAssetsBundle.Empty, preferBundledThumb: true);
        string uri = images.Resolve(new WikiImageRef
        {
            EnemyId = "x",
            File = "y.webp",
            CacheKey = "x/y.webp",
            Uri = "https://ellen.wiki/images/enemies/x/y.webp",
        });
        Assert.AreEqual("https://ellen.wiki/images/enemies/x/y.webp", uri, "没有随包图片时必须回落到在线 URL");
    }
}
