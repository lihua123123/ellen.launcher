using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Ellen.Launcher.Domain.Resistance;

/// <summary>
/// 单个怪物的抗性数据。<see cref="SubHurt"/> 为 <see langword="null"/> 表示该条目
/// 在元数据中缺 <c>BaseValue</c>（88/562），须由调用方回退到
/// <see cref="ResistanceTiers.Default"/>。
/// </summary>
public sealed class MonsterResistance
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>按 <see cref="ResistanceElement"/> 下标排列的 8 项抗性。</summary>
    public double[]? SubHurt { get; init; }

    public bool HasData => SubHurt is not null;

    public double Get(ResistanceElement element)
    {
        return SubHurt is { } values ? values[(int)element] : ResistanceTiers.Default;
    }
}

/// <summary>
/// 读取 <c>Resource/Metadata/Genshin/CHS/Monster.json</c> 的 <c>BaseValue.*SubHurt</c>。
/// 这是抗性的主源（MIT 元数据，已随包），不依赖 <c>EnemyArchives</c>。
/// </summary>
public static class MonsterResistanceReader
{
    public const string SubHurtSuffix = "SubHurt";

    public static IReadOnlyDictionary<string, MonsterResistance> ReadFrom(Stream utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);

        using JsonDocument document = JsonDocument.Parse(utf8Json);
        Dictionary<string, MonsterResistance> result = new(StringComparer.Ordinal);
        ResistanceElement[] elements = Enum.GetValues<ResistanceElement>();

        foreach (JsonElement entry in document.RootElement.EnumerateArray())
        {
            MonsterResistance monster = ReadEntry(entry, elements);

            // 同名条目取「有数据」者；88 条缺 BaseValue 的机关/召唤物与本体可能同名。
            if (!result.TryGetValue(monster.Name, out MonsterResistance? existing)
                || (!existing.HasData && monster.HasData))
            {
                result[monster.Name] = monster;
            }
        }

        return result;
    }

    private static MonsterResistance ReadEntry(JsonElement entry, ResistanceElement[] elements)
    {
        int id = entry.TryGetProperty("Id", out JsonElement idElement) ? idElement.GetInt32() : 0;
        string name = entry.TryGetProperty("Name", out JsonElement nameElement)
            ? nameElement.GetString() ?? string.Empty
            : string.Empty;

        double[]? subHurt = null;
        if (entry.TryGetProperty("BaseValue", out JsonElement baseValue)
            && baseValue.ValueKind == JsonValueKind.Object)
        {
            subHurt = new double[elements.Length];
            foreach (ResistanceElement element in elements)
            {
                subHurt[(int)element] =
                    baseValue.TryGetProperty($"{element}{SubHurtSuffix}", out JsonElement value)
                    && value.TryGetDouble(out double parsed)
                        ? parsed
                        : ResistanceTiers.Default;
            }
        }

        return new MonsterResistance { Id = id, Name = name, SubHurt = subHurt };
    }
}
