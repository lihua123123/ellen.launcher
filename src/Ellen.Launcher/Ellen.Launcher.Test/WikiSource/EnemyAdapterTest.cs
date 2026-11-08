using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.Domain.WikiSource.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

[TestClass]
public class EnemyAdapterTest
{
    private const string ItemJson = """
    {
      "id": "sample", "name": "样例怪", "nameEn": "Sample", "subtitle": "副标题",
      "category": "Legends", "categoryLabel": "地方传奇", "tier": "Boss", "defaultLevel": 100,
      "baseStats": {
        "hp": { "Value": 326.016, "Curve": "HP_2" },
        "atk": { "Value": 70.336, "Curve": "ATK_2" },
        "def": { "Value": 500, "Curve": "DEF_1" }
      },
      "resistances": [
        { "name": "正常", "pyro": 25, "hydro": 25, "anemo": 25, "electro": 25, "dendro": 25, "cryo": 25, "geo": 25, "physical": 25 },
        { "name": "入场", "pyro": "IMM", "hydro": 225, "anemo": 10, "electro": 10, "dendro": 10, "cryo": 10, "geo": 10, "physical": 10, "diffNote": "N5" }
      ],
      "skills": [ { "name": "拍击", "type": "输出", "icon": "skill-1.webp", "description": "造成<c=#ffe599>50%</c>伤害" } ],
      "notes": [ { "text": "虚界护罩", "desc": "<c=#9999ff>200虚界</c>盾" } ],
      "shields": [], "chargebar": [], "energy": { "element": "None", "raw": "", "rows": [ { "hp": 0, "particle": 0, "orb": 0 } ] },
      "levelRows": [ { "label": "剧情", "level": 100, "hp": 1331040, "atk": 13788, "weight": 300, "tenacity": 4000 } ],
      "icon": "avatar.webp", "tags": ["地方传奇"], "images": ["reference.png"]
    }
    """;

    private const string EmptyStatsJson = """
    {
      "id": "empty", "name": "未知数值", "nameEn": "", "subtitle": "",
      "category": "Tps", "categoryLabel": "第三人称射击（TPS）", "tier": "", "defaultLevel": "",
      "baseStats": { "hp": {}, "atk": {}, "def": {} },
      "resistances": [], "skills": [], "notes": [], "shields": [], "chargebar": [], "energy": null,
      "levelRows": [], "icon": "", "tags": [], "images": []
    }
    """;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [TestMethod]
    public void AdaptsCoreFieldsResistancesAndRichText()
    {
        EnemyItemDto dto = JsonSerializer.Deserialize<EnemyItemDto>(ItemJson, Options)!;
        WikiDiagnosticsLog log = new();
        WikiEnemy enemy = new EnemyAdapter(log, strict: true).Adapt(dto);

        Assert.AreEqual("sample", enemy.Id);
        Assert.AreEqual("地方传奇", enemy.CategoryName);
        Assert.AreEqual("首领", enemy.TierName);
        Assert.AreEqual(100, enemy.DefaultLevel);
        Assert.IsNotNull(enemy.BaseValue);
        Assert.AreEqual(326.016, enemy.BaseValue.Hp, 0.0001);
        Assert.AreEqual("DEF_1", enemy.BaseValue.DefCurve);

        Assert.AreEqual(2, enemy.ResistanceStates.Count);
        Assert.IsTrue(enemy.ResistanceStates[0].IsDefault);
        Assert.AreEqual(8, enemy.ResistanceStates[0].Cells.Count);
        Assert.AreEqual("25%", enemy.ResistanceStates[0].Cells[0].ValueText);
        Assert.AreEqual("IMM", enemy.ResistanceStates[1].Cells[0].ValueText);
        Assert.IsTrue(enemy.ResistanceStates[1].Cells[0].IsImmune);
        Assert.AreEqual("225%", enemy.ResistanceStates[1].Cells[1].ValueText);
        Assert.IsTrue(enemy.ResistanceStates[1].Cells[1].IsHigh);
        Assert.AreEqual("N5", enemy.ResistanceStates[1].DiffNote);

        Assert.AreEqual(3, enemy.Skills[0].Description.Count);
        Assert.AreEqual("造成", enemy.Skills[0].Description[0].Text);
        Assert.AreEqual("#ffe599", enemy.Skills[0].Description[1].ColorHex);
        Assert.AreEqual("伤害", enemy.Skills[0].Description[2].Text);
        Assert.IsFalse(WikiRichText.ContainsRawMarkup(enemy.Skills[0].Description));
        Assert.AreEqual("输出", enemy.Skills[0].Tag);
    }

    [TestMethod]
    public void ExtensionSlotsStayNullWhenSourceLacksFields()
    {
        EnemyItemDto dto = JsonSerializer.Deserialize<EnemyItemDto>(ItemJson, Options)!;
        WikiEnemy enemy = new EnemyAdapter().Adapt(dto);

        Assert.IsNull(enemy.Evidence);
        Assert.IsNull(enemy.Mechanics);
        Assert.IsNull(enemy.Variants);
        Assert.IsNull(enemy.Campaign);
        Assert.IsNull(enemy.DisplayName);
        Assert.IsNull(enemy.Skills[0].Tips);
        Assert.IsNull(enemy.Skills[0].Combo);
        Assert.IsNull(enemy.Skills[0].Damage);
        Assert.IsNull(enemy.Skills[0].Dodge);
        Assert.IsNull(enemy.ResistanceStates[0].Evidence);
        Assert.IsNull(enemy.ResistanceStates[0].Condition);
    }

    [TestMethod]
    public void EmptyBaseStatsIsUnknownAndNeverZero()
    {
        EnemyItemDto dto = JsonSerializer.Deserialize<EnemyItemDto>(EmptyStatsJson, Options)!;
        WikiDiagnosticsLog log = new();
        WikiEnemy enemy = new EnemyAdapter(log, strict: true).Adapt(dto);

        Assert.IsNull(enemy.BaseValue);
        Assert.IsTrue(enemy.BaseValueUnknown);
        Assert.IsNull(enemy.DefaultLevel);
        Assert.AreEqual(0, enemy.Images.Count);
        Assert.IsTrue(log.Records.Any(r => r.Kind == "base-stats-unknown"));
    }

    [TestMethod]
    public void ImageRefsAreOnlineWithContentAddressedCacheKey()
    {
        EnemyItemDto dto = JsonSerializer.Deserialize<EnemyItemDto>(ItemJson, Options)!;
        WikiDiagnosticsLog log = new();
        WikiEnemy enemy = new EnemyAdapter(log).Adapt(dto);

        Assert.IsTrue(enemy.Images.Any(i => i.File == "avatar.webp"));
        WikiImageRef avatar = enemy.Images.First(i => i.File == "avatar.webp");
        Assert.AreEqual("https://ellen.wiki/images/enemies/sample/avatar.webp", avatar.Uri);
        Assert.AreEqual("sample/avatar.webp", avatar.CacheKey);
        Assert.IsFalse(avatar.HashAvailable);
        Assert.IsNull(avatar.ContentHash);
        Assert.IsTrue(log.Records.Any(r => r.Kind == "image-hash-missing"));
    }

    [TestMethod]
    public void CurveSourceIsExplicitlyLoggedNeverSilentBundledFallback()
    {
        EnemyItemDto dto = JsonSerializer.Deserialize<EnemyItemDto>(ItemJson, Options)!;
        WikiDiagnosticsLog log = new();
        _ = new EnemyAdapter(log).Adapt(dto);

        WikiDiagnostic curve = log.Records.First(r => r.Kind == "curve-source");
        StringAssert.Contains(curve.Detail, "MonsterCurve");
        StringAssert.Contains(curve.FallbackReason ?? string.Empty, "不静默");
        Assert.AreEqual("ellen.wiki", curve.Source);
    }
}
