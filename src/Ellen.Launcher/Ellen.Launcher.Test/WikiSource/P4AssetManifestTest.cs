using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P4 图片离线：assets manifest 契约、sha256 校验、全量/增量同步、离线解析。
/// 用本地生成的等价 manifest（reports/enemy-assets-manifest.sample60.json）跑真实下载，
/// 上游正式 manifest 到位后只需换文件，不改代码。
/// </summary>
[TestClass]
public class P4AssetManifestTest
{
    private const string DistRoot = "D:/EllenDB/ellen-wiki/dist";
    private const string Reports = "D:/EllenDB/workspace/wiki-migration/reports";
    private const string SampleManifest = Reports + "/enemy-assets-manifest.sample60.json";

    private static string NewCache() => Path.Combine(Path.GetTempPath(), "EllenP4Assets", Guid.NewGuid().ToString("N"));

    private static WikiAssetManifest LoadSample()
    {
        Assert.IsTrue(File.Exists(SampleManifest), $"前置：样本 manifest 不存在（node scripts/build-assets-manifest.mjs --sample=60）：{SampleManifest}");
        WikiAssetManifest? manifest = WikiAssetManifest.LoadFile(SampleManifest);
        Assert.IsNotNull(manifest);
        return manifest!;
    }

    private static HttpWikiSourceOptions Options(HttpMessageHandler handler, string cacheRoot) => new()
    {
        Endpoint = "https://ellen.wiki",
        Handler = handler,
        CacheRoot = cacheRoot,
        MinRequestInterval = TimeSpan.Zero,
        MaxRetries = 0,
    };

    [TestMethod]
    public void ManifestParsesAndMatchesAllowedRefs()
    {
        WikiAssetManifest manifest = LoadSample();
        Assert.AreEqual(WikiAssetManifest.ExpectedSchema, manifest.Dto.Schema);
        Assert.AreEqual(60, manifest.Count);
        Assert.IsTrue(manifest.Bytes > 0);
        Assert.IsTrue(manifest.Dto.Files.All(f => f.Sha256.Length == 64), "每条都必须有 64 位 sha256");
        Assert.IsTrue(manifest.Dto.Files.All(f => f.Size > 0));

        // 用 753 条 item 的真实引用做白名单校验：manifest 不得含 orphan
        HashSet<string> allowed = LoadAllowedKeys();
        WikiAssetManifestValidation validation = manifest.Validate(allowed);
        Assert.IsTrue(validation.SchemaOk);
        Assert.AreEqual(0, validation.OrphanKeys.Count, $"manifest 不得含 orphan：{string.Join(",", validation.OrphanKeys.Take(5))}");
        Assert.AreEqual(0, validation.BadHashKeys.Count);
        Assert.IsTrue(validation.Pass);
    }

    [TestMethod]
    public async Task SyncDownloadsVerifiesHashAndSecondRunIsFullyCached()
    {
        WikiAssetManifest manifest = LoadSample();
        string cacheRoot = NewCache();
        WikiDiagnosticsLog log = new();

        using LocalMirrorHandler handler = new(DistRoot);
        using WikiCacheStore cache = new(cacheRoot);
        using HttpEnemyWebSource source = new(Options(handler, cacheRoot), cache, log);

        WikiImageSyncResult first = await source.SyncImagesAsync(manifest, concurrency: 4).ConfigureAwait(false);
        Assert.AreEqual(manifest.Count, first.Total);
        Assert.AreEqual(manifest.Count, first.Downloaded, $"应全部下载：{first.Downloaded}/{first.Total}");
        Assert.AreEqual(0, first.Failed);
        Assert.AreEqual(0, first.HashRejected, "本地镜像内容必须与 manifest sha 一致");
        Assert.IsTrue(first.Bytes > 0);

        // 第二次：全部命中，不再下载
        WikiImageSyncResult second = await source.SyncImagesAsync(manifest, concurrency: 4).ConfigureAwait(false);
        Assert.AreEqual(manifest.Count, second.AlreadyCached);
        Assert.AreEqual(0, second.Downloaded, "重复同步不得重复下载");

        // 离线解析：WikiImageCache 必须能解析出本地文件 URI
        WikiImageCache images = new(
            manifest.Keys,
            cacheRoot: string.Empty,
            log,
            WikiSourceStamp.FromEllenWiki("test", null, null),
            key => cache.ReadBytes("image:" + key) is null ? null : cache.FilePath("image:" + key));

        EnemyAssetFileDto sample = manifest.Dto.Files[0];
        string uri = images.Resolve(new WikiImageRef { EnemyId = sample.Id, File = sample.File, CacheKey = sample.Key });
        Assert.IsFalse(uri.StartsWith("http", StringComparison.OrdinalIgnoreCase), $"离线时应解析为本地文件：{uri}");
        Assert.IsTrue(File.Exists(new Uri(uri).LocalPath), uri);
        Assert.AreEqual(0, images.Misses, "manifest 内所有引用都应命中本地缓存");
    }

    [TestMethod]
    public async Task TamperedHashIsRejectedAndNotWritten()
    {
        WikiAssetManifest manifest = LoadSample();
        EnemyAssetFileDto target = manifest.Dto.Files[0];
        // 构造一份 sha 被篡改的单条 manifest
        string tamperedJson = $$"""
        {
          "schema": "{{WikiAssetManifest.ExpectedSchema}}",
          "count": 1,
          "bytes": {{target.Size}},
          "files": [
            { "id": "{{target.Id}}", "file": "{{target.File}}", "size": {{target.Size}},
              "sha256": "{{new string('0', 64)}}" }
          ]
        }
        """;
        WikiAssetManifest? tampered = WikiAssetManifest.Parse(System.Text.Encoding.UTF8.GetBytes(tamperedJson));
        Assert.IsNotNull(tampered);

        string cacheRoot = NewCache();
        WikiDiagnosticsLog log = new();
        using LocalMirrorHandler handler = new(DistRoot);
        using WikiCacheStore cache = new(cacheRoot);
        using HttpEnemyWebSource source = new(Options(handler, cacheRoot), cache, log);

        WikiImageSyncResult result = await source.SyncImagesAsync(tampered!).ConfigureAwait(false);

        Assert.AreEqual(1, result.HashRejected, "sha 不符必须拒收");
        Assert.AreEqual(0, result.Downloaded);
        Assert.IsNull(cache.ReadBytes("image:" + target.Key), "拒收的图片不得进缓存");
        Assert.IsTrue(log.Records.Any(d => d.Kind == "image-hash-mismatch"));
    }

    [TestMethod]
    public async Task MissingManifestIsExplicitlyReported()
    {
        string cacheRoot = NewCache();
        WikiDiagnosticsLog log = new();
        /* 用一个空目录当镜像根：manifest 必然 404。
           不要再用真实 dist 来制造「缺失」——A4 交付后 dist 里已经有 assets-manifest.json，
           那时这条用例会变成「假设环境没东西」的假失败。 */
        string emptyRoot = Path.Combine(Path.GetTempPath(), "EllenP4Empty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);
        using LocalMirrorHandler handler = new(emptyRoot);
        using WikiCacheStore cache = new(cacheRoot);
        using HttpEnemyWebSource source = new(Options(handler, cacheRoot), cache, log);

        WikiAssetManifest? manifest = await source.ReadAssetsManifestAsync().ConfigureAwait(false);

        Assert.IsNull(manifest);
        Assert.IsTrue(log.Records.Any(d => d.Kind == "assets-manifest-missing"),
            "manifest 缺失必须显式记录，不能装作离线可用");
    }

    /// <summary>
    /// A4 到样后的真清单必须能被客户端直接解析：schema、4506 条引用覆盖、0 orphan、sha 长度、thumb 可解析。
    /// 这是「上游交付 → 客户端消费」的唯一自动化接缝；清单不存在时用 Inconclusive，不伪装通过。
    /// </summary>
    [TestMethod]
    public void UpstreamManifestParsesAndCoversAllItemRefs()
    {
        const string UpstreamManifest = DistRoot + "/api/enemies/assets-manifest.json";
        if (!File.Exists(UpstreamManifest))
        {
            Assert.Inconclusive("A4 清单未生成（ellen-wiki: npm run enemy-assets && npm run build）");
            return;
        }

        WikiAssetManifest? manifest = WikiAssetManifest.LoadFile(UpstreamManifest);
        Assert.IsNotNull(manifest, "上游清单必须能被 WikiAssetManifest 解析");
        Assert.AreEqual(WikiAssetManifest.ExpectedSchema, manifest!.Dto.Schema);
        Assert.IsTrue(manifest.Count > 0);
        Assert.IsTrue(manifest.Bytes > 0);

        HashSet<string> allowed = LoadAllowedKeys();
        Assert.AreEqual(allowed.Count, manifest.Count, "清单条数必须等于 item 引用数（不许多、不许少）");
        WikiAssetManifestValidation validation = manifest.Validate(allowed);
        Assert.IsTrue(validation.Pass,
            $"清单校验失败：orphan={validation.OrphanKeys.Count} badHash={validation.BadHashKeys.Count} schemaOk={validation.SchemaOk}");
    }

    private static HashSet<string> LoadAllowedKeys()
    {
        string itemsDir = "D:/EllenDB/ellen-wiki/src/data/enemies/items";
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(itemsDir, "*.json"))
        {
            string id = Path.GetFileNameWithoutExtension(path);
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            System.Text.Json.JsonElement root = doc.RootElement;
            void Add(string? file)
            {
                if (!string.IsNullOrWhiteSpace(file)) keys.Add($"{id}/{file}");
            }

            if (root.TryGetProperty("icon", out System.Text.Json.JsonElement icon)) Add(icon.GetString());
            foreach (string field in new[] { "images", "leylineImages" })
            {
                if (!root.TryGetProperty(field, out System.Text.Json.JsonElement arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array) continue;
                foreach (System.Text.Json.JsonElement e in arr.EnumerateArray()) Add(e.GetString());
            }

            if (root.TryGetProperty("skills", out System.Text.Json.JsonElement skills) && skills.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (System.Text.Json.JsonElement s in skills.EnumerateArray())
                {
                    if (s.TryGetProperty("icon", out System.Text.Json.JsonElement si)) Add(si.GetString());
                }
            }

            if (root.TryGetProperty("leyline", out System.Text.Json.JsonElement ley) && ley.ValueKind == System.Text.Json.JsonValueKind.Object
                && ley.TryGetProperty("portrait", out System.Text.Json.JsonElement portrait))
            {
                Add(portrait.GetString());
            }
        }

        return keys;
    }
}
