// P5 753 条目 JSON 随包：离线首屏数据源单测。
// 覆盖：随包存在（753）/ 随包缺失 / 缓存种子化 / 离线优先于回退（不回退 722）/ 真实随包目录 753 条。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.Domain.WikiSource.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

[TestClass]
public class BundledEnemyWebSourceTest
{
    private sealed class EmptyInner : IEnemyWebSource, ICachedEnemyWebSource
    {
        public ValueTask<EnemyWebReadResult> ReadAsync(CancellationToken token = default)
            => ValueTask.FromResult(new EnemyWebReadResult());

        public ValueTask<EnemyWebReadResult> ReadCachedAsync(CancellationToken token = default)
            => ValueTask.FromResult(new EnemyWebReadResult());
    }

    private sealed class StubInner : IEnemyWebSource, ICachedEnemyWebSource
    {
        private readonly EnemyWebReadResult cached;
        public StubInner(EnemyWebReadResult cached) => this.cached = cached;
        public ValueTask<EnemyWebReadResult> ReadAsync(CancellationToken token = default)
            => ValueTask.FromResult(cached);
        public ValueTask<EnemyWebReadResult> ReadCachedAsync(CancellationToken token = default)
            => ValueTask.FromResult(cached);
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "EllenBundleTest", Guid.NewGuid().ToString("N"));

    private static void WriteBundle(string root, int count)
    {
        Directory.CreateDirectory(root);
        List<EnemyIndexEntryDto> enemies = [];
        for (int i = 0; i < count; i++)
        {
            string id = "e" + i;
            enemies.Add(new EnemyIndexEntryDto { Id = id, Name = "E" + i, Category = "Cat" });
            File.WriteAllText(Path.Combine(root, id + ".json"),
                "{\"id\":\"" + id + "\",\"name\":\"E" + i + "\",\"category\":\"Cat\"}");
        }

        File.WriteAllText(Path.Combine(root, "index.json"),
            System.Text.Json.JsonSerializer.Serialize(new EnemyIndexDocumentDto
            {
                Categories = [new EnemyCategoryDto { Key = "Cat", Label = "Cat", Count = count }],
                Enemies = enemies,
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    [TestMethod]
    public async Task BundledPresentReturnsAllItemsWithBundledStamp()
    {
        string root = NewDir();
        WriteBundle(root, 753);
        using WikiCacheStore cache = new(Path.Combine(root, "cache"));
        BundledEnemyWebSource source = new(root, new EmptyInner(), cache, new WikiDiagnosticsLog());

        EnemyWebReadResult result = await source.ReadCachedAsync().ConfigureAwait(false);

        Assert.AreEqual(753, result.Items.Count, "随包 753 必须全部读出");
        Assert.AreEqual(WikiSourceStamp.Bundled753, result.Stamp.Contract, "来源契约必须是 enemy-web-753-bundled");
        Assert.IsFalse(result.Diagnostics.Any(d => d.Kind == "bundle-empty" || d.Kind == "bundle-missing"));
    }

    [TestMethod]
    public async Task BundledPresentSeedsCacheWithEnemyKeys()
    {
        string root = NewDir();
        WriteBundle(root, 5);
        string cacheRoot = Path.Combine(root, "cache");
        using WikiCacheStore cache = new(cacheRoot);
        // 种缓存默认关闭（首启 2.6–2.9s 白烧），显式打开才走旧行为。
        BundledEnemyWebSource source = new(root, new EmptyInner(), cache, new WikiDiagnosticsLog())
        {
            SeedCacheEnabled = true,
        };

        await source.ReadCachedAsync().ConfigureAwait(false);

        // 种子化后缓存里应有 enemy-index.json + 5 个 enemy:<id>。
        Assert.IsNotNull(cache.ReadBytes("enemy-index.json"), "index 应已种入缓存");
        for (int i = 0; i < 5; i++)
        {
            Assert.IsNotNull(cache.ReadBytes("enemy:e" + i), $"enemy:e{i} 应已种入缓存");
        }
    }

    [TestMethod]
    public async Task BundledPresentDoesNotSeedCacheByDefault()
    {
        string root = NewDir();
        WriteBundle(root, 5);
        using WikiCacheStore cache = new(Path.Combine(root, "cache"));
        BundledEnemyWebSource source = new(root, new EmptyInner(), cache, new WikiDiagnosticsLog());

        EnemyWebReadResult result = await source.ReadCachedAsync().ConfigureAwait(false);

        // 753 条照样读出，但不写缓存：随包文件本身即离线基线，再复制一份只会拖慢首启。
        Assert.AreEqual(5, result.Items.Count);
        Assert.IsFalse(source.SeedCacheEnabled, "默认不种缓存");
        Assert.IsNull(cache.ReadBytes("enemy-index.json"), "默认不应把 index 写进缓存");
        for (int i = 0; i < 5; i++)
        {
            Assert.IsNull(cache.ReadBytes("enemy:e" + i), $"默认不应写入 enemy:e{i}");
        }
    }

    [TestMethod]
    public async Task BundledMissingReturnsEmptyAndLogsDiagnostic()
    {
        string root = NewDir(); // 随包目录根本不存在（构建未包含 Web753 的场景）
        // 缓存放到独立目录，避免 WikiCacheStore 构造时把 root 也建出来，扭曲“随包缺失”判定。
        using WikiCacheStore cache = new(Path.Combine(Path.GetTempPath(), "EllenBundleMissing", Guid.NewGuid().ToString("N")));
        WikiDiagnosticsLog log = new();
        BundledEnemyWebSource source = new(root, new EmptyInner(), cache, log);

        EnemyWebReadResult result = await source.ReadCachedAsync().ConfigureAwait(false);

        Assert.AreEqual(0, result.Items.Count, "随包缺失时返回 0 条，由上层决定回退 legacy（不白屏）");
        Assert.IsTrue(log.Records.Any(d => d.Kind == "bundle-missing"), "随包缺失必须显式记录诊断");
    }

    [TestMethod]
    public async Task InnerCacheTakesPriorityOverBundle()
    {
        string root = NewDir();
        WriteBundle(root, 753);
        EnemyWebReadResult innerResult = new()
        {
            Index = new EnemyIndexDocumentDto { Enemies = [new EnemyIndexEntryDto { Id = "inner1" }] },
            Items = new Dictionary<string, EnemyItemDto>(StringComparer.Ordinal)
            {
                ["inner1"] = new EnemyItemDto { Id = "inner1", Name = "Inner" },
            },
        };
        BundledEnemyWebSource source = new(root, new StubInner(innerResult), null, new WikiDiagnosticsLog());

        EnemyWebReadResult result = await source.ReadCachedAsync().ConfigureAwait(false);

        Assert.AreEqual(1, result.Items.Count, "HTTP 缓存有数必须优先于随包");
        Assert.IsTrue(result.Items.ContainsKey("inner1"));
        Assert.IsFalse(result.Items.ContainsKey("e0"), "不应混入随包条目");
    }

    [TestMethod]
    public async Task RealBundleDirectoryYields753()
    {
        // 指向构建产物里的真实随包目录（由 tools/EllenWikiSync/sync_bundle.py 生成）。
        string root = @"D:/EllenDB/ellen-launcher/Installer/Publish/Resource/EnemyArchives/Web753";
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "index.json")))
        {
            Assert.Inconclusive("真实随包目录不存在，先运行 sync_bundle.py 生成 Web753");
            return;
        }

        using WikiCacheStore cache = new(Path.Combine(Path.GetTempPath(), "EllenBundleReal", Guid.NewGuid().ToString("N")));
        BundledEnemyWebSource source = new(root, new EmptyInner(), cache, new WikiDiagnosticsLog());

        EnemyWebReadResult result = await source.ReadCachedAsync().ConfigureAwait(false);

        Assert.AreEqual(753, result.Items.Count, "真实随包必须 DTO 级 753/753");
        Assert.AreEqual(WikiSourceStamp.Bundled753, result.Stamp.Contract);
        Assert.IsFalse(result.Diagnostics.Any(d => d.Kind == "bundle-item-parse"), "随包条目不应有解析失败");
    }
}
