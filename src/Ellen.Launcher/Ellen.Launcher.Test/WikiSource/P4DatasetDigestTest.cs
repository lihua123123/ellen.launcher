using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P4-3：manifest.datasets 分数据集增量。digest 未变 + 缓存完整时必须整段跳过 C2 网络请求；
/// 上游未提供 datasets 时退化为文件级 ETag（仍只下变文件），并显式记录。
/// </summary>
[TestClass]
public class P4DatasetDigestTest
{
    private sealed class SyntheticHandler : HttpMessageHandler
    {
        public string DatasetDigest { get; set; } = "enemies-digest-1";
        public bool IncludeDatasets { get; set; } = true;
        public int ItemCount { get; set; } = 3;
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            string path = request.RequestUri!.AbsolutePath;

            if (path.EndsWith("/api/launcher/manifest.json", StringComparison.Ordinal))
            {
                string datasets = IncludeDatasets
                    ? ",\"datasets\":{\"enemies\":{\"digest\":\"" + DatasetDigest + "\",\"version\":\"1\",\"count\":" + ItemCount + "}}"
                    : string.Empty;
                return Json("{\"name\":\"test\",\"generatedAt\":\"2026-11-08T00:00:00Z\",\"digest\":\"manifest-digest\","
                    + "\"locale\":\"CHS\",\"counts\":{},\"files\":[]" + datasets + "}");
            }

            if (path.EndsWith("/api/enemies/index.json", StringComparison.Ordinal))
            {
                StringBuilder sb = new("[");
                for (int i = 1; i <= ItemCount; i++)
                {
                    if (i > 1) sb.Append(',');
                    sb.Append($$"""{"id":"e{{i}}","name":"E{{i}}"}""");
                }

                sb.Append(']');
                return Json(sb.ToString());
            }

            if (path.Contains("/api/enemies/", StringComparison.Ordinal))
            {
                string id = path[(path.LastIndexOf('/') + 1)..^5];
                return Json($$"""{"id":"{{id}}","name":"{{id}}"}""");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private static Task<HttpResponseMessage> Json(string body)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }

    private static HttpWikiSourceOptions Options(HttpMessageHandler handler, string cacheRoot) => new()
    {
        Endpoint = "https://ellen.wiki",
        Handler = handler,
        CacheRoot = cacheRoot,
        MinRequestInterval = TimeSpan.Zero,
        MaxRetries = 0,
    };

    private static string NewCache() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "EllenP4Dataset", Guid.NewGuid().ToString("N"));

    [TestMethod]
    public async Task UnchangedDatasetDigestSkipsAllC2Requests()
    {
        string cacheRoot = NewCache();
        using SyntheticHandler handler = new();
        WikiDiagnosticsLog log = new();
        using WikiCacheStore cache = new(cacheRoot);
        HttpWikiSourceOptions options = Options(handler, cacheRoot);
        using HttpLauncherSnapshotSource snapshot = new(options, cache, log);
        using HttpEnemyWebSource source = new(options, cache, log, snapshot);

        EnemyWebReadResult first = await source.ReadAsync().ConfigureAwait(false);
        Assert.IsFalse(first.DatasetCacheHit, "首次必须真读");
        Assert.AreEqual(3, first.Items.Count);
        Assert.AreEqual(5, handler.RequestCount, "manifest + index + 3 items");

        // 模拟重启：同一缓存新实例，digest 未变
        using SyntheticHandler second = new() { DatasetDigest = "enemies-digest-1" };
        using WikiCacheStore cache2 = new(cacheRoot);
        WikiDiagnosticsLog log2 = new();
        HttpWikiSourceOptions options2 = Options(second, cacheRoot);
        using HttpLauncherSnapshotSource snapshot2 = new(options2, cache2, log2);
        using HttpEnemyWebSource source2 = new(options2, cache2, log2, snapshot2);
        EnemyWebReadResult hit = await source2.ReadAsync().ConfigureAwait(false);

        Assert.IsTrue(hit.DatasetCacheHit, "digest 未变且缓存完整必须整段跳过");
        Assert.AreEqual(3, hit.Items.Count);
        Assert.AreEqual(1, second.RequestCount, "只允许读 1 次 manifest，C2 请求必须为 0");
        Assert.IsTrue(log2.Records.Any(d => d.Kind == "dataset-cache-hit"));
    }

    [TestMethod]
    public async Task ChangedDatasetDigestTriggersResync()
    {
        string cacheRoot = NewCache();
        using (SyntheticHandler first = new())
        {
            using WikiCacheStore cache = new(cacheRoot);
            WikiDiagnosticsLog log = new();
            HttpWikiSourceOptions options = Options(first, cacheRoot);
            using HttpLauncherSnapshotSource snapshot = new(options, cache, log);
            using HttpEnemyWebSource source = new(options, cache, log, snapshot);
            _ = await source.ReadAsync().ConfigureAwait(false);
        }

        using SyntheticHandler second = new() { DatasetDigest = "enemies-digest-2" };
        using WikiCacheStore cache2 = new(cacheRoot);
        WikiDiagnosticsLog log2 = new();
        HttpWikiSourceOptions options2 = Options(second, cacheRoot);
        using HttpLauncherSnapshotSource snapshot2 = new(options2, cache2, log2);
        using HttpEnemyWebSource source2 = new(options2, cache2, log2, snapshot2);
        EnemyWebReadResult result = await source2.ReadAsync().ConfigureAwait(false);

        Assert.IsFalse(result.DatasetCacheHit, "digest 变了必须重新校验");
        Assert.AreEqual(3, result.Items.Count);
        Assert.IsTrue(second.RequestCount >= 4, $"应重新请求 index+items，实际 {second.RequestCount}");
    }

    [TestMethod]
    public async Task MissingDatasetsFallsBackToPerFileEtagWithExplicitNote()
    {
        string cacheRoot = NewCache();
        using SyntheticHandler handler = new() { IncludeDatasets = false };
        WikiDiagnosticsLog log = new();
        using WikiCacheStore cache = new(cacheRoot);
        HttpWikiSourceOptions options = Options(handler, cacheRoot);
        using HttpLauncherSnapshotSource snapshot = new(options, cache, log);
        using HttpEnemyWebSource source = new(options, cache, log, snapshot);

        EnemyWebReadResult result = await source.ReadAsync().ConfigureAwait(false);

        Assert.AreEqual(3, result.Items.Count);
        Assert.IsFalse(result.DatasetCacheHit);
        Assert.IsTrue(log.Records.Any(d => d.Kind == "datasets-missing"), "缺 datasets 必须显式记录，不能装作有 digest");
        Assert.IsTrue(WikiDiagnosticClassifier.IsSourceDegradation("datasets-missing") == false, "缺 digest 是字段告警，不是源降级");
    }
}
