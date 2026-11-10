using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P4 缓存完整性：原子写、hash 校验、索引损坏恢复、LRU/磁盘上限、全量自检。
/// 全部用真实文件系统跑，不 mock。
/// </summary>
[TestClass]
public class WikiCacheIntegrityTest
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "EllenCacheTest", Guid.NewGuid().ToString("N"));

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [TestMethod]
    public void PutWritesAtomicallyAndLeavesNoTempFile()
    {
        string root = NewRoot();
        using WikiCacheStore store = new(root);
        store.Put("enemy:a", Bytes("{\"id\":\"a\"}"), "etag-a");

        string path = store.FilePath("enemy:a");
        Assert.IsTrue(File.Exists(path));
        // 目录里不能残留 .tmp-
        Assert.AreEqual(0, Directory.EnumerateFiles(Path.GetDirectoryName(path)!, ".tmp-*").Count());
        CollectionAssert.AreEqual(Bytes("{\"id\":\"a\"}"), store.ReadBytes("enemy:a"));
    }

    [TestMethod]
    public void InterruptedTempFilesAreSweptOnStartup()
    {
        string root = NewRoot();
        using (WikiCacheStore first = new(root))
        {
            first.Put("enemy:a", Bytes("payload"), null);
            string path = first.FilePath("enemy:a");
            // 模拟被杀进程留下的半成品
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!, ".tmp-abc123"), "half");
            File.WriteAllText(Path.Combine(root, "cache-index.json.tmp-xyz"), "half");
        }

        using WikiCacheStore second = new(root);
        int swept = second.SweepTempFiles();
        Assert.AreEqual(2, swept);
        Assert.AreEqual(0, Directory.EnumerateFiles(root, "*.tmp-*", SearchOption.AllDirectories).Count());
        // 正常文件不受影响
        CollectionAssert.AreEqual(Bytes("payload"), second.ReadBytes("enemy:a"));
    }

    [TestMethod]
    public void PutVerifiedRejectsHashMismatch()
    {
        using WikiCacheStore store = new(NewRoot());
        string good = WikiCacheStore.Hash(Bytes("good"));

        Assert.IsFalse(store.PutVerified("image:x/avatar.webp", Bytes("tampered"), null, good), "hash 不符必须拒收");
        Assert.IsNull(store.ReadBytes("image:x/avatar.webp"));
        Assert.IsFalse(store.TryGet("image:x/avatar.webp", out _), "拒收后不得写入索引");

        Assert.IsTrue(store.PutVerified("image:x/avatar.webp", Bytes("good"), null, good));
        Assert.IsNotNull(store.ReadBytes("image:x/avatar.webp"));
    }

    [TestMethod]
    public async Task CorruptedIndexRecoversFromBackup()
    {
        string root = NewRoot();
        using (WikiCacheStore store = new(root))
        {
            store.Put("enemy:a", Bytes("a"), null);
            await store.SaveAsync().ConfigureAwait(false);       // 第 1 次：index 落盘
            store.Put("enemy:b", Bytes("b"), null);
            await store.SaveAsync().ConfigureAwait(false);       // 第 2 次：.bak = 含 a 的版本
        }

        File.WriteAllText(Path.Combine(root, "cache-index.json"), "{ this is not json");

        using WikiCacheStore recovered = new(root);
        Assert.IsTrue(recovered.RecoveryNote?.Contains("bak", StringComparison.Ordinal) == true, recovered.RecoveryNote ?? "(无恢复说明)");
        CollectionAssert.AreEqual(Bytes("a"), recovered.ReadBytes("enemy:a"));
    }

    [TestMethod]
    public void CorruptedFileIsDetectedAndTreatedAsMiss()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("enemy:a", Bytes("original"), null);
        File.WriteAllBytes(store.FilePath("enemy:a"), Bytes("tampered-on-disk"));

        Assert.IsNull(store.ReadBytes("enemy:a"), "磁盘内容与索引 sha 不符时必须判定为 miss，交给上层重下");
    }

    [TestMethod]
    public void PruneRemovesUnreferencedKeepsImagePrefixAndProtectsInfra()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("manifest.json", Bytes("m"), null);
        store.Put("enemy-index.json", Bytes("i"), null);
        store.Put("enemy:a", Bytes("a"), null);
        store.Put("enemy:stale", Bytes("s"), null);
        store.Put("image:x/avatar.webp", Bytes("img"), null);

        WikiCachePruneResult result = store.Prune(new WikiCachePruneOptions
        {
            KeepKeys = ["manifest.json", "enemy-index.json", "enemy:a"],
            KeepPrefixes = ["image:"],
            MaxBytes = 0,
        });

        Assert.AreEqual(1, result.RemovedUnreferenced, "只应删掉 enemy:stale");
        Assert.IsNull(store.ReadBytes("enemy:stale"));
        Assert.IsNotNull(store.ReadBytes("enemy:a"));
        Assert.IsNotNull(store.ReadBytes("image:x/avatar.webp"), "image: 前缀不在数据集中，但必须按前缀保留");
        Assert.IsNotNull(store.ReadBytes("manifest.json"));
    }

    [TestMethod]
    public void PruneEnforcesMaxBytesByLruButNeverDropsInfra()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("manifest.json", Bytes(new string('m', 100)), null);
        store.Put("enemy:old", Bytes(new string('o', 400)), null);
        store.Put("enemy:new", Bytes(new string('n', 400)), null);

        // 手动把 old 的访问时间调到过去
        store.TryGet("enemy:old", out WikiCacheEntry oldEntry);
        oldEntry.LastAccessUtc = DateTime.UtcNow.AddDays(-2).ToString("O");
        store.TryGet("enemy:new", out WikiCacheEntry newEntry);
        newEntry.LastAccessUtc = DateTime.UtcNow.ToString("O");

        WikiCachePruneResult result = store.Prune(new WikiCachePruneOptions
        {
            KeepKeys = ["manifest.json", "enemy:old", "enemy:new"],
            MaxBytes = 600, // 900 字节 → 必须淘汰到 600 以下
        });

        Assert.IsTrue(result.RemovedByLru >= 1);
        Assert.IsNotNull(store.ReadBytes("manifest.json"), "基础设施不参与 LRU 淘汰");
        Assert.IsNotNull(store.ReadBytes("enemy:new"), "最近访问的应保留");
        Assert.IsTrue(result.BytesAfter <= 600, $"淘汰后 {result.BytesAfter} 应 ≤ 600");
    }

    [TestMethod]
    public void VerifyAllReportsMissingFileAndRepairsIndex()
    {
        using WikiCacheStore store = new(NewRoot());
        store.Put("enemy:a", Bytes("a"), null);
        store.Put("enemy:b", Bytes("b"), null);
        File.Delete(store.FilePath("enemy:b"));

        WikiCacheVerifyResult result = store.VerifyAll();

        Assert.AreEqual(2, result.Checked);
        Assert.AreEqual(1, result.Failures.Count);
        Assert.AreEqual("enemy:b", result.Failures[0].Key);
        Assert.AreEqual("文件缺失", result.Failures[0].Reason);
        Assert.IsFalse(store.TryGet("enemy:b", out _), "自检后失效条目必须从索引移除");
        Assert.IsTrue(store.TryGet("enemy:a", out _));
    }
}
