using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P3 HTTP 源演练：用 LocalMirrorHandler 把 /api/** 映射到 ellen-wiki/dist/api/**，
/// 不联网、真跑 753 条 DTO 反序列化、增量缓存、失败降级、UA 合规。
/// </summary>
[TestClass]
public class HttpWikiSourceTest
{
    // LocalMirrorHandler 把 URL 的 /api/** 映射到 <distRoot>/api/**，所以根是 dist/。
    private const string DistRoot = "D:/EllenDB/ellen-wiki/dist";

    private static string NewCache() => Path.Combine(Path.GetTempPath(), "EllenP3Test", Guid.NewGuid().ToString("N"));

    private static HttpWikiSourceOptions Options(HttpMessageHandler handler, string cacheRoot) => new()
    {
        Endpoint = "https://ellen.wiki",
        Handler = handler,
        CacheRoot = cacheRoot,
        MinRequestInterval = TimeSpan.Zero, // 单测不限速
        RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        MaxRetries = 1,
    };

    [TestMethod]
    public async Task HttpSourceDeserializesAll753AtDtoLevel()
    {
        string cacheRoot = NewCache();
        using LocalMirrorHandler handler = new(DistRoot);
        using HttpEnemyWebSource source = new(Options(handler, cacheRoot), new WikiCacheStore(cacheRoot));

        EnemyWebReadResult result = await source.ReadAsync().ConfigureAwait(false);

        Assert.AreEqual(753, result.Items.Count, "HTTP 源必须 DTO 级 753/753，不能复用文件级数字");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "item-parse"), "含 duration union 也不能丢条目");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "enemy-count-mismatch"));
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "item-fallback"));
        Assert.IsTrue(handler.RequestCount >= 754, "index + 753 items 都要请求");
    }

    [TestMethod]
    public async Task SecondRunDoesNotRedownloadUnchangedFiles()
    {
        string cacheRoot = NewCache();
        using LocalMirrorHandler first = new(DistRoot);
        WikiCacheStats coldStats;
        using (HttpEnemyWebSource s1 = new(Options(first, cacheRoot), new WikiCacheStore(cacheRoot)))
        {
            Assert.AreEqual(753, (await s1.ReadAsync().ConfigureAwait(false)).Items.Count);
            coldStats = s1.Stats;
        }

        int coldRequests = first.RequestCount;

        using LocalMirrorHandler second = new(DistRoot);
        WikiDiagnosticsLog log = new();
        WikiCacheStore cache = new(cacheRoot);
        using HttpEnemyWebSource s2 = new(Options(second, cacheRoot), cache, log);
        EnemyWebReadResult result = await s2.ReadAsync().ConfigureAwait(false);

        Assert.AreEqual(753, result.Items.Count);
        Assert.AreEqual(coldRequests, second.RequestCount, "二次启动仍要问 ETag，但不应重新下载内容");
        Assert.IsTrue(s2.Stats.NotModified >= 753, "753 个 item 应全部 304 NotModified");
        Assert.AreEqual(0, s2.Stats.Downloads, "二次启动 0 下载");
        Assert.AreEqual(0, s2.Stats.BytesDownloaded, "二次启动 0 字节下载");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "item-fallback"));
        Assert.IsTrue(coldStats.Downloads > 0, "冷启动确实下载过");
    }

    [TestMethod]
    public async Task DigestChangeOnlyDownloadsChangedFiles()
    {
        string cacheRoot = NewCache();
        using (LocalMirrorHandler first = new(DistRoot))
        using (HttpLauncherSnapshotSource snap = new(Options(first, cacheRoot), new WikiCacheStore(cacheRoot)))
        {
            _ = await snap.ReadAsync().ConfigureAwait(false);
        }

        using (LocalMirrorHandler h2 = new(DistRoot))
        using (HttpLauncherSnapshotSource snap2 = new(Options(h2, cacheRoot), new WikiCacheStore(cacheRoot)))
        {
            _ = await snap2.ReadAsync().ConfigureAwait(false);
            Assert.AreEqual(0, snap2.Stats.Downloads, "manifest 未变 + ETag 304 → 0 下载");
            Assert.AreEqual(1, snap2.Stats.NotModified);
        }
    }

    [TestMethod]
    public async Task NetworkFailureFallsBackToCacheExplicitly()
    {
        string cacheRoot = NewCache();
        using (LocalMirrorHandler first = new(DistRoot))
        using (HttpEnemyWebSource s1 = new(Options(first, cacheRoot), new WikiCacheStore(cacheRoot)))
        {
            Assert.AreEqual(753, (await s1.ReadAsync().ConfigureAwait(false)).Items.Count);
        }

        // 第二次：handler 全部 500，应从缓存读出 753 条并逐条记录降级
        using FailingHandler failing = new();
        WikiDiagnosticsLog log = new();
        using HttpEnemyWebSource s2 = new(Options(failing, cacheRoot), new WikiCacheStore(cacheRoot), log);
        EnemyWebReadResult result = await s2.ReadAsync().ConfigureAwait(false);

        Assert.AreEqual(753, result.Items.Count, "网络失败时用缓存，不空屏");
        Assert.IsTrue(result.Diagnostics.Any(d => d.Kind == "item-fallback"), "降级必须有显式诊断");
        Assert.IsTrue(result.Diagnostics.Any(d => d.Kind == "index-fallback"));
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "item-parse"), "缓存内容仍应能 DTO 解析");
    }

    [TestMethod]
    public async Task ComplianceHeadersArePresent()
    {
        using RecordingHandler recording = new();
        HttpWikiSourceOptions options = Options(recording, NewCache());
        using WikiHttpClient client = new(options);

        _ = await client.GetAsync(options.ManifestPath).ConfigureAwait(false);

        Assert.IsTrue(recording.UserAgent.Contains("EllenLauncher"), "必须带显式 UA");
        Assert.IsTrue(recording.UserAgent.Contains("ellen.wiki"), "UA 需含来源说明");
        Assert.AreEqual("https://ellen.wiki/", recording.Referer);
    }

    /// <summary>
    /// 首次无缓存 + 断网：必须返回 0 条并留下显式诊断，由上层显示「后台同步中 / 保持 legacy」，
    /// 不能抛异常、不能白屏、不能拿旧 722 冒充 753。
    /// </summary>
    [TestMethod]
    public async Task EmptyCacheAndOfflineReturnsZeroWithExplicitDiagnostics()
    {
        string cacheRoot = NewCache();
        using FailingHandler failing = new();
        WikiDiagnosticsLog log = new();
        using WikiCacheStore cache = new(cacheRoot);
        using HttpEnemyWebSource source = new(Options(failing, cacheRoot), cache, log);

        // 纯缓存读：空缓存必须 0 条，且不产生网络请求
        EnemyWebReadResult cached = await source.ReadCachedAsync().ConfigureAwait(false);
        Assert.AreEqual(0, cached.Items.Count, "空缓存首屏应返回 0 条");
        Assert.AreEqual(0, failing.RequestCount, "纯缓存读不得触网");

        // 完整读：网络全 503，仍必须 0 条 + 显式诊断
        EnemyWebReadResult full = await source.ReadAsync().ConfigureAwait(false);
        Assert.AreEqual(0, full.Items.Count);
        Assert.IsTrue(full.Diagnostics.Any(d => d.Kind is "http-failure" or "index-fallback"),
            "首次无缓存断网必须留下显式诊断");
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string UserAgent { get; private set; } = string.Empty;
        public string Referer { get; private set; } = string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UserAgent = request.Headers.UserAgent.ToString();
            Referer = request.Headers.Referrer?.ToString() ?? string.Empty;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"digest\":\"x\",\"files\":[]}"),
            });
        }
    }
}
