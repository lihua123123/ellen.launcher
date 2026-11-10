using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P3 演练：冷/热启动、首次/增量同步、图片按需缓存、flag 持久化与回滚，
/// 真实跑一遍并写出 P3 报告供验收（不是手工填数字）。
/// </summary>
[TestClass]
public class WikiP3HarnessTest
{
    private const string DistRoot = "D:/EllenDB/ellen-wiki/dist";
    private const string ReportRoot = "D:/EllenDB/workspace/wiki-migration/reports";

    private sealed class FileFlagStore(string path) : IWikiFlagStore
    {
        public string? Read()
        {
            try { return File.Exists(path) ? File.ReadAllText(path).Trim() : null; }
            catch (IOException) { return null; }
        }

        public void Write(string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, value);
        }
    }

    /// <summary>模拟断网：所有请求返回 503（可重试错误，重试次数为 0 时直接失败）。</summary>
    private sealed class OfflineHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    [TestMethod]
    public async Task EmitP3RuntimeReport()
    {
        Assert.IsTrue(Directory.Exists(DistRoot), $"前置：本地镜像不存在 {DistRoot}");
        string cacheRoot = Path.Combine(Path.GetTempPath(), "EllenP3Report", Guid.NewGuid().ToString("N"));
        string flagPath = Path.Combine(cacheRoot, "ui-source.txt");

        WikiP3Report report = await WikiP3Harness.RunAsync(DistRoot, cacheRoot, new FileFlagStore(flagPath)).ConfigureAwait(false);

        Directory.CreateDirectory(ReportRoot);
        JsonSerializerOptions options = new() { WriteIndented = true };
        File.WriteAllText(Path.Combine(ReportRoot, "P3-runtime-report.json"), JsonSerializer.Serialize(report, options));

        // 诊断页载荷单独落一份，便于 P4/P5 直接对接 UI
        File.WriteAllText(Path.Combine(ReportRoot, "P3-diagnostics-payload.json"), JsonSerializer.Serialize(new
        {
            generatedAt = report.GeneratedAt,
            note = "诊断页要展示的字段；来自真实同步后的 WikiSyncState，不是静态文案。",
            page = report.DiagnosticsPage,
        }, options));

        File.WriteAllText(Path.Combine(ReportRoot, "P3-rollback-drill.json"), JsonSerializer.Serialize(new
        {
            generatedAt = report.GeneratedAt,
            drill = report.Flag,
            note = "flag 持久化经真实文件读写；运行中切换与重启保持由新建 flag 实例验证。",
        }, options));

        Console.WriteLine($"P3 Pass={report.Pass} Failures={string.Join(" | ", report.Failures)}");
        Console.WriteLine($"冷启动 legacy={report.Startup.ColdStartLegacyFirstScreenMs}ms web={report.Startup.ColdStartWebFirstScreenMs}ms 热启动={report.Startup.HotStartWebFirstScreenMs}ms");
        Console.WriteLine($"首次同步={report.Sync.FirstSyncMs}ms 下载={report.Sync.FirstSyncDownloads}; 增量={report.Sync.IncrementalSyncMs}ms 304={report.Sync.IncrementalNotModified} 下载={report.Sync.IncrementalDownloads}");
        Console.WriteLine($"图片抽样={report.Images.Sampled} 冷命中={report.Images.ColdHitRate}% 热命中={report.Images.WarmHitRate}% 失败={report.Images.FailureRate}%");

        Assert.IsTrue(report.Pass, string.Join(" | ", report.Failures));
        Assert.AreEqual(753, report.Sync.ItemsIncremental);
        Assert.AreEqual(0, report.Sync.IncrementalDownloads, "二次同步不得重复下载未变文件");
        Assert.IsTrue(report.Sync.IncrementalNotModified > 700, "二次同步应大量 304");
        Assert.IsTrue(report.Flag.DefaultIsLegacy);
        Assert.IsFalse(report.Startup.StartupBlockedBySync);
        Assert.AreEqual(753, report.Startup.HotStartFullItemsRead, "热启动应能从缓存读全 753 条 item");
        Assert.IsTrue(report.Startup.HotStartWebFirstScreenMs < 200, $"热启动首屏只读 manifest+index，实测 {report.Startup.HotStartWebFirstScreenMs}ms");
        Assert.AreEqual(100.0, report.Images.WarmHitRate, "同批图片第二次必须全部命中缓存");
        Assert.AreEqual(0.0, report.Images.FailureRate, "本地镜像下图片不应有失败");
        Assert.IsFalse(string.IsNullOrEmpty(report.DiagnosticsPage.Digest), "诊断页必须有 digest");
    }

    /// <summary>网络失败时必须显式降级到缓存并记录，不得静默、不得抛到启动路径。</summary>
    [TestMethod]
    public async Task NetworkFailureFallsBackToCacheWithDiagnostics()
    {
        string cacheRoot = Path.Combine(Path.GetTempPath(), "EllenP3Offline", Guid.NewGuid().ToString("N"));
        WikiSourceFeatureFlag flag = new(new FileFlagStore(Path.Combine(cacheRoot, "flag.txt")));
        flag.Switch(WikiSourceFeatureFlag.EnemyWeb753);

        // 先在线跑一次建立缓存
        using (LocalMirrorHandler online = new(DistRoot))
        {
            HttpWikiSourceOptions options = new()
            {
                Endpoint = "https://ellen.wiki",
                Handler = online,
                CacheRoot = cacheRoot,
                MinRequestInterval = TimeSpan.Zero,
                MaxRetries = 0,
            };
            WikiCacheStore cache = new(cacheRoot);
            WikiDiagnosticsLog log = new();
            using HttpLauncherSnapshotSource snapshot = new(options, cache, log);
            using HttpEnemyWebSource source = new(options, cache, log, snapshot);
            EnemyWebReadResult first = await source.ReadAsync().ConfigureAwait(false);
            Assert.AreEqual(753, first.Items.Count);
        }

        // 再断网：全部请求失败，必须用缓存降级
        using OfflineHandler offlineHandler = new();
        HttpWikiSourceOptions offlineOptions = new()
        {
            Endpoint = "https://ellen.wiki",
            Handler = offlineHandler,
            CacheRoot = cacheRoot,
            MinRequestInterval = TimeSpan.Zero,
            MaxRetries = 0,
        };
        WikiCacheStore offlineCache = new(cacheRoot);
        WikiDiagnosticsLog offlineLog = new();
        using HttpLauncherSnapshotSource offlineSnapshot = new(offlineOptions, offlineCache, offlineLog);
        using HttpEnemyWebSource offlineSource = new(offlineOptions, offlineCache, offlineLog, offlineSnapshot);

        WikiSyncCoordinator coordinator = new(
            flag,
            offlineLog,
            token => offlineSource.ReadAsync(token),
            () => offlineSource.Stats);
        await coordinator.PrimeAsync(null).ConfigureAwait(false);
        WikiSyncState state = await coordinator.SyncNowAsync().ConfigureAwait(false);

        Assert.AreEqual(753, state.ItemCount, "断网时必须用缓存继续服务 753 条");
        Assert.IsFalse(state.Timing.StartupBlockedBySync);
        Assert.IsTrue(state.Diagnostics.Count > 0, "降级必须有诊断记录，不能静默");
        Assert.IsTrue(state.Diagnostics.Any(d => d.Kind is "snapshot-fallback" or "item-fallback" or "http-failure"));

        Directory.CreateDirectory(ReportRoot);
        File.WriteAllText(
            Path.Combine(ReportRoot, "P3-offline-fallback.json"),
            JsonSerializer.Serialize(new
            {
                generatedAt = DateTime.UtcNow.ToString("O"),
                note = "断网后用缓存继续提供 753 条，并记录诊断；P3 图片仍是在线+缓存，离线冷启动不具备可用性。",
                itemCount = state.ItemCount,
                source = state.Source,
                fallbackReason = state.FallbackReason,
                diagnosticTotal = state.Diagnostics.Count,
                diagnosticsByKind = state.Diagnostics
                    .GroupBy(d => d.Kind)
                    .Select(g => new
                    {
                        kind = g.Key,
                        count = g.Count(),
                        samples = g.Take(3).Select(d => d.Detail),
                    }),
            }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
