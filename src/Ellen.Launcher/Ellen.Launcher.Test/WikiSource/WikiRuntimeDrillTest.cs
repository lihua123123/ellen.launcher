using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// P3 真实 DI 演练：flag 持久化（重启保持）、运行中切换、回滚、
/// 深链 83/83 在 DI 装配的数据源下复验（不只 harness）。
/// </summary>
[TestClass]
public class WikiRuntimeDrillTest
{
    private sealed class MemoryFlagStore : IWikiFlagStore
    {
        public string? Value { get; set; }
        public string? Read() => Value;
        public void Write(string value) => Value = value;
    }

    [TestMethod]
    public void FlagPersistsAcrossRestartAndRejectsDirtyValue()
    {
        MemoryFlagStore store = new();
        WikiSourceFeatureFlag first = new(store);
        Assert.AreEqual(WikiSourceFeatureFlag.Legacy722Bundled, first.UiSource, "默认必须仍是 legacy");

        Assert.IsTrue(first.Switch(WikiSourceFeatureFlag.EnemyWeb753));
        Assert.IsTrue(first.IsWebMode);
        Assert.AreEqual(WikiSourceFeatureFlag.EnemyWeb753, store.Value, "切换必须持久化");

        // 模拟重启：用同一个 store 新建 flag 实例
        WikiSourceFeatureFlag restarted = new(store);
        Assert.IsTrue(restarted.IsWebMode, "重启后必须保持 enemy-web-753");

        Assert.IsFalse(restarted.Switch("unknown"));
        Assert.IsTrue(restarted.IsWebMode, "未知值不得改变状态");

        Assert.IsTrue(restarted.Switch(WikiSourceFeatureFlag.Legacy722Bundled));
        WikiSourceFeatureFlag restartedAgain = new(store);
        Assert.IsTrue(restartedAgain.IsLegacyMode, "回滚后重启仍保持 legacy");

        // 脏设置不得让启动变成 web
        store.Value = "garbage";
        Assert.IsTrue(new WikiSourceFeatureFlag(store).IsLegacyMode);
    }

    [TestMethod]
    public async Task RuntimeSwitchAndRollbackWithRealSource()
    {
        string dist = "D:/EllenDB/ellen-wiki/dist";
        string cache = Path.Combine(Path.GetTempPath(), "EllenP3Drill", Guid.NewGuid().ToString("N"));
        MemoryFlagStore store = new();
        WikiSourceFeatureFlag flag = new(store);
        WikiDiagnosticsLog log = new();
        WikiCacheStore cacheStore = new(cache);
        LocalMirrorHandler handler = new(dist);
        HttpWikiSourceOptions options = new()
        {
            Endpoint = "https://ellen.wiki",
            Handler = handler,
            CacheRoot = cache,
            MinRequestInterval = TimeSpan.Zero,
            MaxRetries = 1,
            RetryBaseDelay = TimeSpan.FromMilliseconds(1),
        };
        using HttpEnemyWebSource source = new(options, cacheStore, log);

        // 运行中切换：legacy → web → 用真实 HTTP 源读取 → 回滚
        Assert.IsTrue(flag.Switch(WikiSourceFeatureFlag.EnemyWeb753));
        Assert.IsTrue(flag.IsWebMode);
        EnemyWebReadResult result = await source.ReadAsync().ConfigureAwait(false);
        Assert.AreEqual(753, result.Items.Count);
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "item-parse"));

        Assert.IsTrue(flag.Switch(WikiSourceFeatureFlag.Legacy722Bundled));
        Assert.IsTrue(flag.IsLegacyMode, "回滚后回到 legacy");
        Assert.IsTrue(flag.RollbackDrill());
        Assert.IsTrue(flag.IsLegacyMode, "回滚演练结束仍为默认");
    }

    /// <summary>深链 83/83 在真实 DI 装配的 HTTP 源数据上复验（不是 harness 造的数据）。</summary>
    [TestMethod]
    public async Task DeepLink83Of83UnderRealDiSource()
    {
        string dist = "D:/EllenDB/ellen-wiki/dist";
        string cache = Path.Combine(Path.GetTempPath(), "EllenP3Drill", Guid.NewGuid().ToString("N"));
        using LocalMirrorHandler handler = new(dist);
        HttpWikiSourceOptions options = new()
        {
            Endpoint = "https://ellen.wiki",
            Handler = handler,
            CacheRoot = cache,
            MinRequestInterval = TimeSpan.Zero,
            MaxRetries = 1,
        };
        using HttpEnemyWebSource source = new(options, new WikiCacheStore(cache));
        EnemyWebReadResult web = await source.ReadAsync().ConfigureAwait(false);
        Assert.AreEqual(753, web.Items.Count, "前置：HTTP 源必须 DTO 级 753/753");

        List<AliasRecord> aliases = LoadAliases();
        HashSet<string> groupSlugs = [.. aliases.GroupBy(a => a.NewId).Where(g => g.Count() > 1).Select(g => g.Key)];
        List<AliasRecord> members = [.. aliases.Where(a => groupSlugs.Contains(a.NewId))];
        Assert.AreEqual(34, groupSlugs.Count);
        Assert.AreEqual(83, members.Count);

        AliasResolver resolver = new(aliases);
        WikiDeepLink deepLink = new(resolver);
        int exact = 0;
        foreach (AliasRecord record in members)
        {
            (string oldSlug, string oldKey, string _, bool oldDegraded) = deepLink.Resolve(record.OldId, null);
            (string slug, string key, string _, bool degraded) = deepLink.Resolve(record.NewId, record.StateKey);
            bool hit = !oldDegraded && !degraded
                && oldSlug == record.NewId && oldKey == record.StateKey
                && slug == record.NewId && key == record.StateKey
                && web.Items.ContainsKey(slug); // 真实数据源里存在该 slug
            if (hit) exact++;
        }

        Assert.AreEqual(83, exact, "真实 DI 数据源下必须 83/83 精确还原、0 静默降级");
    }

    private static List<AliasRecord> LoadAliases()
    {
        string path = "D:/EllenDB/workspace/wiki-migration/reports/alias-statekey-map.json";
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        List<AliasRecord> list = [];
        foreach (System.Text.Json.JsonElement r in doc.RootElement.GetProperty("records").EnumerateArray())
        {
            list.Add(new AliasRecord
            {
                OldId = r.GetProperty("oldId").GetString() ?? string.Empty,
                NewId = r.GetProperty("slug").GetString() ?? string.Empty,
                StateKey = r.GetProperty("stateKey").GetString() ?? string.Empty,
                Rule = "statekey-map",
                MatchedVia = r.GetProperty("origin").GetString() ?? string.Empty,
            });
        }

        return list;
    }
}
