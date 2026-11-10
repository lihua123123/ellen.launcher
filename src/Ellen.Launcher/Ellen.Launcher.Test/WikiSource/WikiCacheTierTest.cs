using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P5 缓存分层：层内配额互不挤占、全局上限按层优先级淘汰、infra 永不被淘汰。
/// 用真实文件系统跑，不 mock。
/// </summary>
[TestClass]
public class WikiCacheTierTest
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "EllenCacheTierTest", Guid.NewGuid().ToString("N"));

    private static byte[] Payload(int size) => Encoding.UTF8.GetBytes(new string('x', size));

    private static readonly WikiCacheTierLimit Infra = new()
    {
        Name = WikiCacheTiers.Infra,
        Prefixes = ["manifest.json", "enemy-index.json"],
        MaxBytes = 1024,
        Protected = true,
        EvictionPriority = 0,
    };

    private static readonly WikiCacheTierLimit Items = new()
    {
        Name = WikiCacheTiers.Items,
        Prefixes = ["enemy:"],
        MaxBytes = 150,
        EvictionPriority = 20,
    };

    private static readonly WikiCacheTierLimit Images = new()
    {
        Name = WikiCacheTiers.Images,
        Prefixes = ["image:"],
        MaxBytes = 250,
        EvictionPriority = 30,
    };

    private static readonly WikiCacheTierLimit Thumbs = new()
    {
        Name = WikiCacheTiers.Thumbnails,
        Prefixes = ["thumb:"],
        MaxBytes = 150,
        EvictionPriority = 10,
    };

    [TestMethod]
    public void TierQuotaEvictsOnlyInsideItsOwnTier()
    {
        using WikiCacheStore store = new(NewRoot());
        for (int i = 0; i < 3; i++)
        {
            store.Put($"enemy:a{i}", Payload(100), null);
            store.Put($"image:a{i}", Payload(100), null);
        }

        WikiCachePruneResult result = store.Prune(new WikiCachePruneOptions
        {
            KeepKeys = [],
            KeepPrefixes = ["image:", "enemy:"],
            TierLimits = [Infra, Items, Images, Thumbs],
        });

        // items 层 300 → 150，应只剩 1 个（层内 LRU）；images 层 300 → 250，只剩 2 个。
        Assert.AreEqual(1, result.Tiers.Single(t => t.Name == WikiCacheTiers.Items).Files);
        Assert.AreEqual(2, result.Tiers.Single(t => t.Name == WikiCacheTiers.Images).Files);
        Assert.AreEqual(3, result.RemovedByTier);
        Assert.AreEqual(0, result.RemovedByLru);
    }

    [TestMethod]
    public void GlobalCapEvictsImagesBeforeItems()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("enemy:a", Payload(100), null);
        store.Put("enemy:b", Payload(100), null);
        for (int i = 0; i < 3; i++)
        {
            store.Put($"image:a{i}", Payload(100), null);
        }

        WikiCacheTierLimit generousItems = new()
        {
            Name = WikiCacheTiers.Items, Prefixes = ["enemy:"], MaxBytes = 10_000, EvictionPriority = 20,
        };
        WikiCacheTierLimit generousImages = new()
        {
            Name = WikiCacheTiers.Images, Prefixes = ["image:"], MaxBytes = 10_000, EvictionPriority = 30,
        };

        WikiCachePruneResult result = store.Prune(new WikiCachePruneOptions
        {
            KeepKeys = [],
            KeepPrefixes = ["image:", "enemy:"],
            MaxBytes = 300,
            TierLimits = [Infra, generousItems, generousImages],
        });

        // 总 500 → 300：先淘汰 2 张详情图；条目数据一条都不该掉。
        Assert.AreEqual(2, result.RemovedByLru);
        Assert.AreEqual(2, result.Tiers.Single(t => t.Name == WikiCacheTiers.Items).Files);
        Assert.AreEqual(1, result.Tiers.Single(t => t.Name == WikiCacheTiers.Images).Files);
        Assert.IsTrue(store.CachedKeys().Count(k => k.StartsWith("enemy:", StringComparison.Ordinal)) == 2);
    }

    [TestMethod]
    public void ThumbsAreEvictedAfterItemsButBeforeInfra()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("manifest.json", Payload(400), null);
        store.Put("enemy:a", Payload(200), null);
        store.Put("thumb:a", Payload(200), null);
        store.Put("image:a", Payload(200), null);

        WikiCacheTierLimit noTierCap = new()
        {
            Name = "unused", Prefixes = ["__none__"], MaxBytes = 10_000,
        };

        WikiCachePruneResult result = store.Prune(new WikiCachePruneOptions
        {
            KeepKeys = [],
            KeepPrefixes = ["image:", "thumb:", "enemy:"],
            MaxBytes = 600,
            TierLimits =
            [
                Infra,
                new WikiCacheTierLimit { Name = WikiCacheTiers.Items, Prefixes = ["enemy:"], MaxBytes = 10_000, EvictionPriority = 20 },
                new WikiCacheTierLimit { Name = WikiCacheTiers.Images, Prefixes = ["image:"], MaxBytes = 10_000, EvictionPriority = 30 },
                new WikiCacheTierLimit { Name = WikiCacheTiers.Thumbnails, Prefixes = ["thumb:"], MaxBytes = 10_000, EvictionPriority = 10 },
                noTierCap,
            ],
        });

        // 1000 → 600：先 image(30)，再 items(20) 即达标；thumbs(10) 不应被牺牲；infra 受保护永远不删。
        Assert.IsTrue(File.Exists(store.FilePath("manifest.json")));
        Assert.AreEqual(0, result.Tiers.Single(t => t.Name == WikiCacheTiers.Images).Files);
        Assert.AreEqual(0, result.Tiers.Single(t => t.Name == WikiCacheTiers.Items).Files);
        Assert.AreEqual(1, result.Tiers.Single(t => t.Name == WikiCacheTiers.Thumbnails).Files);
        Assert.AreEqual(2, result.RemovedByLru);
    }

    [TestMethod]
    public void ProtectedInfraIsNeverEvictedEvenWhenOverGlobalCap()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("manifest.json", Payload(900), null);
        store.Put("image:a", Payload(100), null);

        store.Prune(new WikiCachePruneOptions
        {
            KeepKeys = [],
            KeepPrefixes = ["image:"],
            MaxBytes = 100,
            TierLimits = [Infra, Items, Images],
        });

        Assert.IsTrue(File.Exists(store.FilePath("manifest.json")));
        Assert.IsFalse(File.Exists(store.FilePath("image:a")));
    }

    [TestMethod]
    public void DefaultTiersFitInsideTotalBudget()
    {
        IReadOnlyList<WikiCacheTierLimit> tiers = WikiCacheTiers.Defaults(1L * 1024 * 1024 * 1024);
        long sum = tiers.Sum(t => t.MaxBytes);
        Assert.IsTrue(sum <= 1L * 1024 * 1024 * 1024, $"分层限额合计 {sum} 超过总预算");
        Assert.IsTrue(tiers.Single(t => t.Name == WikiCacheTiers.Infra).Protected);
        // 详情图是全局上限时最先被淘汰的层。
        Assert.IsTrue(tiers.Single(t => t.Name == WikiCacheTiers.Images).EvictionPriority
            > tiers.Single(t => t.Name == WikiCacheTiers.Items).EvictionPriority);
    }

    [TestMethod]
    public void ClassifyUsesKeyPrefixNotPath()
    {
        Assert.AreEqual(WikiCacheTiers.Items, WikiCacheTiers.Classify("enemy:foo"));
        Assert.AreEqual(WikiCacheTiers.Images, WikiCacheTiers.Classify("image:foo/bar.png"));
        Assert.AreEqual(WikiCacheTiers.Thumbnails, WikiCacheTiers.Classify("thumb:foo/bar.webp"));
        Assert.AreEqual(WikiCacheTiers.Infra, WikiCacheTiers.Classify("manifest.json"));
    }

    [TestMethod]
    public void UsageReportsLimitPercent()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("image:a", Payload(100), null);
        store.Put("image:b", Payload(100), null);

        WikiCacheTierUsage usage = store.GetTierUsage([Infra, Items, Images]).Single(t => t.Name == WikiCacheTiers.Images);
        Assert.AreEqual(2, usage.Files);
        Assert.AreEqual(200, usage.Bytes);
        Assert.AreEqual(250, usage.LimitBytes);
        Assert.AreEqual(80.0, usage.UsagePercent);
    }
}
