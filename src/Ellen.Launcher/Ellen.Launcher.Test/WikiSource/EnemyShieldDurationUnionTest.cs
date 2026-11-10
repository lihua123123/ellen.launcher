using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.Domain.WikiSource.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ellen.Launcher.Test.WikiSource;

/// <summary>
/// ADR R2-1 / enemy-item-v1.1：shields[].duration 是 string|number union。
/// P1 冻结清单误登记为 string，导致 40 个 item 反序列化抛 JsonException（713/753）。
/// 本测试用 DTO 级断言锁定：两种类型都能解析、adapter 归一正确、753/753 全量可读。
/// </summary>
[TestClass]
public class EnemyShieldDurationUnionTest
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    [TestMethod]
    public void DurationAsStringIsAccepted()
    {
        const string json = """{"id":"a","name":"A","shields":[{"name":"盾","description":"","elementType":"","duration":"18"}]}""";
        EnemyItemDto dto = JsonSerializer.Deserialize<EnemyItemDto>(json, Options)!;

        Assert.AreEqual(JsonValueKind.String, dto.Shields[0].Duration!.Value.ValueKind);
        Assert.AreEqual("18", dto.Shields[0].Duration!.Value.GetString());
    }

    [TestMethod]
    public void DurationAsNumberIsAccepted()
    {
        const string json = """{"id":"b","name":"B","shields":[{"name":"盾","description":"","elementType":"","duration":18}]}""";
        EnemyItemDto dto = JsonSerializer.Deserialize<EnemyItemDto>(json, Options)!;

        Assert.AreEqual(JsonValueKind.Number, dto.Shields[0].Duration!.Value.ValueKind);
        Assert.AreEqual(18d, dto.Shields[0].Duration!.Value.GetDouble());
    }

    [TestMethod]
    public void DurationAsEmptyStringOrNullIsAccepted()
    {
        EnemyItemDto empty = JsonSerializer.Deserialize<EnemyItemDto>(
            """{"id":"c","name":"C","shields":[{"duration":""}]}""", Options)!;
        EnemyItemDto absent = JsonSerializer.Deserialize<EnemyItemDto>(
            """{"id":"d","name":"D","shields":[{"name":"盾"}]}""", Options)!;

        Assert.AreEqual(JsonValueKind.String, empty.Shields[0].Duration!.Value.ValueKind);
        Assert.IsNull(absent.Shields[0].Duration);
    }

    [TestMethod]
    public void AdapterNormalizesBothTypesToSameText()
    {
        EnemyAdapter adapter = new();
        EnemyItemDto stringItem = JsonSerializer.Deserialize<EnemyItemDto>(
            """{"id":"a","name":"A","shields":[{"name":"盾","duration":"18"}]}""", Options)!;
        EnemyItemDto numberItem = JsonSerializer.Deserialize<EnemyItemDto>(
            """{"id":"b","name":"B","shields":[{"name":"盾","duration":18}]}""", Options)!;

        Assert.AreEqual("18", adapter.Adapt(stringItem).Shields[0].Duration);
        Assert.AreEqual("18", adapter.Adapt(numberItem).Shields[0].Duration, "number 必须归一成与 string 一致的展示文本");
        string? emptyDuration = adapter.Adapt(JsonSerializer.Deserialize<EnemyItemDto>(
            """{"id":"c","name":"C","shields":[{"duration":""}]}""", Options)!).Shields[0].Duration;
        Assert.IsTrue(string.IsNullOrEmpty(emptyDuration), "空值留空，禁止填 0");
    }

    /// <summary>DTO 级 753/753：不是数文件，而是真实反序列化，且诊断里不得有 item-parse。</summary>
    [TestMethod]
    public async Task All753ItemsDeserializeAtDtoLevel()
    {
        string root = "D:/EllenDB/ellen-wiki/src/data/enemies";
        int expected = Directory.EnumerateFiles(Path.Combine(root, "items"), "*.json").Count();
        Assert.AreEqual(753, expected, "源文件数基线");

        FileEnemyWebSource source = new(root);
        EnemyWebReadResult result = await source.ReadAsync().ConfigureAwait(false);

        Assert.AreEqual(753, result.Items.Count, "DTO 必须真正反序列化 753 条，P1 的 713 是静默丢条目");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "item-parse"),
            string.Join("; ", result.Diagnostics.Where(d => d.Kind == "item-parse").Select(d => d.Detail).Take(3)));

        int stringDuration = 0;
        int numberDuration = 0;
        foreach (EnemyItemDto dto in result.Items.Values)
        {
            foreach (EnemyShieldDto shield in dto.Shields)
            {
                if (!shield.Duration.HasValue) continue;
                if (shield.Duration.Value.ValueKind == JsonValueKind.String) stringDuration++;
                if (shield.Duration.Value.ValueKind == JsonValueKind.Number) numberDuration++;
            }
        }

        Assert.AreEqual(208, stringDuration, "类型直方图基线");
        Assert.AreEqual(47, numberDuration, "类型直方图基线");
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Kind == "item-parse"));
    }
}
