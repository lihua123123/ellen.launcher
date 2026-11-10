using System;
using System.Collections.Generic;
using System.Linq;
using Ellen.Launcher.Domain.WikiSource;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

[TestClass]
public class AliasResolverTest
{
    private static List<AliasRecord> Records() =>
    [
        new AliasRecord { OldId = "Shaman_LostSamachurl", NewId = "mystifying-megachurl", Rule = "exact-name", MatchedVia = "name:神秘大丘", StateKey = "default" },
        new AliasRecord { OldId = "GiantChess_King_Dummy", NewId = "门扉前的弈局", Rule = "wiki-alias", MatchedVia = "aliases:升扬之王", StateKey = "state:shengyang", StateLabel = "升扬之王" },
        new AliasRecord { OldId = "Dahaka_S1_Water_S2_Ice", NewId = "azhdaha", Rule = "wiki-alias", MatchedVia = "aliases:若陀龙王·水冰", StateKey = "state:shuibing", StateLabel = "若陀龙王·水冰" },
        new AliasRecord { OldId = "Gastrobot_Leyline", NewId = "creator-s-pride", Rule = "leyline-alias", MatchedVia = "leyline.name:重拳出击鸭·重甲武库", StateKey = "leyline:0", StateLabel = "重拳出击鸭·重甲武库" },
    ];

    [TestMethod]
    public void ExactNameUsesDefaultStateKey()
    {
        AliasResolution result = new AliasResolver(Records()).Resolve("Shaman_LostSamachurl");

        Assert.AreEqual("mystifying-megachurl", result.Slug);
        Assert.AreEqual("slug", result.Strategy);
        Assert.AreEqual("default", result.StateKey);
        Assert.AreEqual(string.Empty, result.StateLabel);
        Assert.IsFalse(result.Degraded);
    }

    [TestMethod]
    public void ManyToOneUsesSlugPlusStateKey()
    {
        AliasResolution result = new AliasResolver(Records()).Resolve("GiantChess_King_Dummy");

        Assert.AreEqual("门扉前的弈局", result.Slug);
        Assert.AreEqual("slug+stateKey", result.Strategy);
        Assert.AreEqual("state:shengyang", result.StateKey);
        Assert.AreEqual("升扬之王", result.StateLabel);
        Assert.IsFalse(result.Degraded);
    }

    [TestMethod]
    public void StateLabelIsDisplayOnlyAndMayDuplicate()
    {
        // 两个不同 slug 可以用同一个 stateLabel；解析键仍唯一。
        List<AliasRecord> records = Records();
        records.Add(new AliasRecord { OldId = "Other_Body", NewId = "all-devouring-narwhal", Rule = "wiki-alias", MatchedVia = "aliases:本体", StateKey = "state:ben", StateLabel = "本体" });
        records.Add(new AliasRecord { OldId = "Chess_Body", NewId = "门扉前的弈局", Rule = "wiki-alias", MatchedVia = "aliases:本体", StateKey = "state:ben", StateLabel = "本体" });

        StateKeyValidation validation = AliasResolver.ValidateStateKeys(records.Select(r => (r.NewId, r.StateKey)));
        Assert.IsTrue(validation.Unique, "stateLabel 重复不影响 (slug,stateKey) 唯一性");
    }

    [TestMethod]
    public void UnknownOldIdDegradesWithDiagnosticLog()
    {
        WikiDiagnosticsLog log = new();
        AliasResolution result = new AliasResolver(Records(), log).Resolve("Unknown_Id");

        Assert.IsTrue(result.Degraded);
        Assert.AreEqual("legacy-internal-name", result.Strategy);
        Assert.AreEqual("Unknown_Id", result.Slug);
        Assert.IsTrue(log.Records.Any(r => r.Kind == "alias-migration" && r.FallbackReason is not null));
    }

    [TestMethod]
    public void DeepLinkUnknownStateKeyDegradesExplicitly()
    {
        WikiDiagnosticsLog log = new();
        AliasResolution result = new AliasResolver(Records(), log).ResolveDeepLink("门扉前的弈局", "state:not-exist");

        Assert.IsTrue(result.Degraded);
        Assert.AreEqual("slug-default", result.Strategy);
        Assert.AreEqual("default", result.StateKey);
        Assert.IsTrue(log.Records.Any(r => r.Kind == "alias-migration"));
    }

    [TestMethod]
    public void DeepLinkKnownStateKeyIsExact()
    {
        AliasResolution result = new AliasResolver(Records()).ResolveDeepLink("门扉前的弈局", "state:shengyang");

        Assert.IsFalse(result.Degraded);
        Assert.AreEqual("slug+stateKey", result.Strategy);
        Assert.AreEqual("state:shengyang", result.StateKey);
    }

    [TestMethod]
    public void StateKeyValidationDetectsDuplicatesAndFreezesCompositeKey()
    {
        StateKeyValidation unique = AliasResolver.ValidateStateKeys([("a", "state:p1"), ("b", "state:p1")]);
        Assert.IsTrue(unique.Unique, "跨 item 同名 key 由 slug 区分");

        StateKeyValidation duplicated = AliasResolver.ValidateStateKeys([("a", "state:p1"), ("a", "state:p1")]);
        Assert.IsFalse(duplicated.Unique);
        Assert.AreEqual(1, duplicated.Duplicates.Count);

        Assert.AreEqual("azhdaha::state:shuibing", StateKeyValidation.FrozenKey("azhdaha", "state:shuibing"));
    }
}
