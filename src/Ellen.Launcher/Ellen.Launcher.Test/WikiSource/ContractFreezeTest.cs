using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.Domain.WikiSource.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>enemy-item-v1 冻结：DTO 字段集合必须与 P0 冻结清单一致，增减走 ADR 变更。</summary>
[TestClass]
public class ContractFreezeTest
{
    private static readonly string[] FrozenFields =
    [
        "id", "name", "nameEn", "subtitle", "category", "categoryLabel", "tier", "defaultLevel",
        "baseStats", "resistances", "elementMastery", "multiplayerGroup", "weight", "skills",
        "notes", "shields", "chargebar", "energy", "levelRows", "icon", "tags",
        "leyline", "images", "leylineImages", "aliases", "states", "defaultState", "statVariants",
    ];

    private static readonly string[] ExtensionSlots =
        ["evidence", "mechanics", "variants", "campaign", "displayName"];

    private static IReadOnlyList<string> JsonNames(Type type) =>
    [.. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
        .Where(n => n is not null)
        .Select(n => n!)];

    [TestMethod]
    public void EnemyItemDtoMatchesEnemyItemV1()
    {
        IReadOnlyList<string> names = JsonNames(typeof(EnemyItemDto));

        Assert.AreEqual(28, FrozenFields.Length);
        Assert.AreEqual(33, names.Count, "28 冻结字段 + 5 个 item 级扩展槽位");
        foreach (string field in FrozenFields)
        {
            Assert.IsTrue(names.Contains(field), $"DTO 缺冻结字段 {field}");
        }

        foreach (string slot in ExtensionSlots)
        {
            Assert.IsTrue(names.Contains(slot), $"DTO 缺扩展槽位 {slot}");
        }
    }

    /// <summary>enemy-item-v1.1：duration 必须是 JsonElement? 才能容纳 string|number union（ADR R2-1）。</summary>
    [TestMethod]
    public void ShieldDurationUsesJsonElementForUnion()
    {
        System.Reflection.PropertyInfo? property = typeof(EnemyShieldDto).GetProperty(nameof(EnemyShieldDto.Duration));
        Assert.IsNotNull(property);
        Assert.AreEqual(typeof(System.Text.Json.JsonElement?), property!.PropertyType,
            "改成 string? 会让 47 个 number 值抛 JsonException，退回 713/753");

        // 读冻结清单比对，避免「常量 == 常量」的恒真断言，同时真正校验 C# 与清单同步。
        string path = "D:/EllenDB/workspace/wiki-migration/reports/enemy-item-v1.1.json";
        Assert.IsTrue(System.IO.File.Exists(path), "需先运行 scripts/scan-type-stability.mjs");
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
        System.Text.Json.JsonElement root = doc.RootElement;
        Assert.AreEqual(EnemyItemSchema.SchemaVersion, root.GetProperty("schema").GetString());
        Assert.AreEqual(EnemyItemSchema.Supersedes, root.GetProperty("supersedes").GetString());
        StringAssert.Contains(EnemyItemSchema.Supersedes, "enemy-item-v1");
    }

    /// <summary>全量类型直方图的 union 必须全部登记；未登记即视为契约漏洞。</summary>
    [TestMethod]
    public void TypeStabilityReportHasNoUnregisteredUnion()
    {
        string path = "D:/EllenDB/workspace/wiki-migration/reports/type-stability-report.json";
        Assert.IsTrue(System.IO.File.Exists(path), "需先运行 scripts/scan-type-stability.mjs");
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
        System.Text.Json.JsonElement root = doc.RootElement;

        Assert.AreEqual(753, root.GetProperty("itemCount").GetInt32());
        Assert.IsTrue(root.GetProperty("acceptance").GetProperty("pass").GetBoolean(), "存在未登记 union");
        Assert.AreEqual(0, root.GetProperty("summary").GetProperty("unregisteredUnionCount").GetInt32());
        Assert.IsTrue(root.GetProperty("registeredUnions").TryGetProperty("shields[].duration", out _));
    }

    [TestMethod]
    public void NestedExtensionSlotsArePresent()
    {
        IReadOnlyList<string> resistance = JsonNames(typeof(EnemyResistanceStateDto));
        Assert.IsTrue(resistance.Contains("evidence"));
        Assert.IsTrue(resistance.Contains("condition"));

        IReadOnlyList<string> skill = JsonNames(typeof(EnemySkillDto));
        foreach (string slot in new[] { "tips", "combo", "damage", "dodge" })
        {
            Assert.IsTrue(skill.Contains(slot), $"技能缺扩展槽位 {slot}");
        }
    }

    [TestMethod]
    public void TwoDtosAreStrictlySeparated()
    {
        Assert.AreEqual("Ellen.Launcher.Domain.WikiSource.Contracts", typeof(EnemyItemDto).Namespace);
        Assert.AreEqual(typeof(EnemyItemDto).Namespace, typeof(LauncherManifestDto).Namespace);

        // 两套 DTO 不得互相引用：属性类型里不能出现对方命名空间的类型。
        Assert.IsFalse(typeof(EnemyItemDto).GetProperties().Any(p => p.PropertyType.Namespace?.StartsWith("Ellen.Launcher.Domain.WikiSource.Contracts.Launcher", StringComparison.Ordinal) == true));
        Assert.IsFalse(typeof(LauncherManifestDto).GetProperties().Any(p => p.PropertyType.Name.Contains("Enemy", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CurveContractIsStrictAndDef1IsType12()
    {
        Assert.AreEqual(12, CurveValidation.DefenseCurveType);

        CurveValidation missing = CurveContract.Validate(new LauncherManifestDto { Files = [] });
        Assert.IsFalse(missing.MonsterCurvePresentInZip);
        Assert.IsNotNull(missing.Alert);
        StringAssert.Contains(missing.Alert, "不静默");

        CurveValidation present = CurveContract.Validate(new LauncherManifestDto
        {
            Files = [new LauncherFileDto { Path = "MonsterCurve.json" }],
        });
        Assert.IsTrue(present.MonsterCurvePresentInZip);
        Assert.IsNull(present.Alert);
    }
}
