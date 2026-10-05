using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ellen.Launcher.Domain.Resistance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.Resistance;

[TestClass]
public class MonsterResistanceReaderTest
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Monster.json");

    private static readonly IReadOnlyDictionary<string, MonsterResistance> Monsters = Load();

    private static IReadOnlyDictionary<string, MonsterResistance> Load()
    {
        using FileStream stream = File.OpenRead(FixturePath);
        return MonsterResistanceReader.ReadFrom(stream);
    }

    [TestMethod]
    public void MetadataCarriesResistanceForMostMonsters()
    {
        // 独立复算：直接读原始 JSON，不复用读取器，避免"验证实现"而非"验证事实"。
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(FixturePath));
        JsonElement[] entries = [.. document.RootElement.EnumerateArray()];
        int withBaseValue = entries.Count(e => e.TryGetProperty("BaseValue", out JsonElement b) && b.ValueKind == JsonValueKind.Object);

        Assert.AreEqual(562, entries.Length);
        Assert.AreEqual(474, withBaseValue);
    }

    [TestMethod]
    public void EveryBaseValueEntryCarriesAllEightElements()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(FixturePath));
        string[] expected = [.. Enum.GetValues<ResistanceElement>().Select(e => $"{e}{MonsterResistanceReader.SubHurtSuffix}")];

        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("BaseValue", out JsonElement baseValue) || baseValue.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (string key in expected)
            {
                Assert.IsTrue(baseValue.TryGetProperty(key, out _), $"{entry.GetProperty("Name").GetString()} 缺 {key}");
            }
        }
    }

    [TestMethod]
    [DataRow("雷丘丘萨满", ResistanceElement.Elec, 0.5)]
    [DataRow("水丘丘萨满", ResistanceElement.Water, 0.5)]
    [DataRow("幻形豕兽·水", ResistanceElement.Fire, -0.4)]
    [DataRow("岩盾丘丘暴徒", ResistanceElement.Physical, 0.3)]
    [DataRow("历经百战的十六倍曼陀草", ResistanceElement.Grass, 1.35)]
    [DataRow("草史莱姆", ResistanceElement.Physical, 0.1)]
    public void KnownMonstersExposeExpectedResistance(string name, ResistanceElement element, double expected)
    {
        Assert.IsTrue(Monsters.TryGetValue(name, out MonsterResistance? monster), $"未读到 {name}");
        Assert.IsTrue(monster.HasData, $"{name} 缺 BaseValue");
        Assert.AreEqual(expected, monster.Get(element));
    }

    [TestMethod]
    public void MonstersWithoutBaseValueFallBackToDefaultTier()
    {
        Assert.IsTrue(Monsters.TryGetValue("机关·算力增幅器·荒", out MonsterResistance? monster));
        Assert.IsFalse(monster.HasData);
        Assert.AreEqual(ResistanceTiers.Default, monster.Get(ResistanceElement.Physical));
    }

    [TestMethod]
    public void RealizedDomainStaysInsideFormulaSegments()
    {
        // 实测取值域必须落在 k(r) 已覆盖的三段内，出现新档位即说明分段或数据需要复核。
        double[] realized = [-0.5, -0.4, -0.2, 0, 0.1, 0.15, 0.2, 0.25, 0.3, 0.4, 0.5, 0.6, 0.7, 0.9, 1.35, 2];

        foreach (MonsterResistance monster in Monsters.Values.Where(m => m.HasData))
        {
            foreach (ResistanceElement element in Enum.GetValues<ResistanceElement>())
            {
                Assert.Contains(monster.Get(element), realized, $"{monster.Name} 的 {element} 抗性不在实测档位内");
            }
        }
    }
}
