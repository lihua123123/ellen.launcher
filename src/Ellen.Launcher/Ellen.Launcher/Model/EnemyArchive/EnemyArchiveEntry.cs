// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Ellen.Launcher.Model.EnemyArchive;

/// <summary>
/// 敌人档案（抗性 / 技能 / 机制），来源于随应用发布的
/// <c>Resource/EnemyArchives/EnemyArchives.json</c>。
/// <para>
/// 与元数据（<c>Monster.json</c>）互补：元数据的 <c>*SubHurt</c> 只是通用占位值，
/// 档案提供准确的抗性（含“某机制下不同抗性”的多状态）与社区整理的技能信息。
/// </para>
/// </summary>
public sealed class EnemyArchiveEntry
{
    /// <summary>
    /// 匹配键，优先使用元数据的 <c>MonsterName</c>（如 <c>Slime_Fire_02</c>）。
    /// </summary>
    [JsonPropertyName("monsterName")]
    public string? MonsterName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("aliases")]
    public ImmutableArray<string> Aliases { get; set; } = [];

    /// <summary>分类，如「普通」「精英」「BOSS」。</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>重量 / 韧性，如 <c>60/20</c>。</summary>
    [JsonPropertyName("weight")]
    public string? Weight { get; set; }

    [JsonPropertyName("energy")]
    public EnemyArchiveEnergy? Energy { get; set; }

    /// <summary>不同档位的基础数值（如「地方传奇」/「历经百战」）。</summary>
    [JsonPropertyName("variants")]
    public ImmutableArray<EnemyArchiveVariant> Variants { get; set; } = [];

    [JsonPropertyName("resistances")]
    public EnemyArchiveResistances? Resistances { get; set; }

    [JsonPropertyName("mechanics")]
    public ImmutableArray<EnemyArchiveMechanic> Mechanics { get; set; } = [];

    [JsonPropertyName("skills")]
    public ImmutableArray<EnemyArchiveSkill> Skills { get; set; } = [];

    public bool HasEnergy => Energy is { Rows.Length: > 0 };

    public bool HasVariants => !Variants.IsDefaultOrEmpty;

    public bool HasResistances => Resistances is { Values: not null };

    public bool HasMechanics => !Mechanics.IsDefaultOrEmpty;

    public bool HasSkills => !Skills.IsDefaultOrEmpty;

    /// <summary>
    /// 是否与元数据条目匹配（用于把档案挂到正确的 <c>Monster</c> 上）。
    /// </summary>
    public bool Matches(string? monsterName, string? name, string? title)
    {
        if (!string.IsNullOrWhiteSpace(MonsterName)
            && !string.IsNullOrWhiteSpace(monsterName)
            && string.Equals(MonsterName, monsterName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (string candidate in Aliases.IsDefaultOrEmpty ? [Name ?? string.Empty] : Aliases.Add(Name ?? string.Empty))
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (string.Equals(candidate, name, StringComparison.Ordinal)
                || string.Equals(candidate, title, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>产球（元素能量）。原文记法里 <c>P</c> = 该血量阈值产出元素晶球，<c>O</c> = 产出元素微粒。</summary>
public sealed class EnemyArchiveEnergy
{
    /// <summary>解析原文记法的分组：如 <c>PP[66,33],PO[0]</c> → 两组。<b>不能用逗号直接切分</b>（方括号里也有逗号）。</summary>
    private static readonly Regex GroupRegex = new(
        "(?<element>[A-Za-z])(?<kind>[PpOo])\\[(?<hps>[0-9,\\s]+)\\]",
        RegexOptions.Compiled);

    /// <summary>原始记法，如 <c>PP[60,0]</c> 或 <c>PP[66,33],PO[0]</c>。</summary>
    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("rows")]
    public ImmutableArray<EnemyArchiveEnergyRow> Rows { get; set; } = [];

    private ImmutableArray<EnemyArchiveEnergyCell>? cells;

    /// <summary>界面用的展示行；掉落数字带上各自分组的元素键（无色则不上色）。</summary>
    public ImmutableArray<EnemyArchiveEnergyCell> Cells
    {
        get
        {
            if (cells is { } cached)
            {
                return cached;
            }

            ImmutableArray<(string? Key, char Kind, int Hp)> groups = ParseGroups(Raw);
            ImmutableArray<EnemyArchiveEnergyCell> built = Rows.IsDefaultOrEmpty
                ? []
                :
                [
                    .. Rows.Select(row => new EnemyArchiveEnergyCell
                    {
                        HpText = row.HpText,
                        ParticleText = row.ParticleText,
                        OrbText = row.OrbText,
                        ParticleElementKey = ElementKeyOf(groups, row.Hp, 'O'),
                        OrbElementKey = ElementKeyOf(groups, row.Hp, 'P'),
                    }),
                ];

            cells = built;
            return built;
        }
    }

    /// <summary>
    /// 拆解原文记法（如 <c>PP[66,33],PO[0]</c>）：首字母是元素，第二位是掉落类型
    /// （<c>P</c>=元素晶球 / <c>O</c>=元素微粒），方括号内是血量阈值。
    /// </summary>
    private static ImmutableArray<(string? Key, char Kind, int Hp)> ParseGroups(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        ImmutableArray<(string? Key, char Kind, int Hp)>.Builder builder = ImmutableArray.CreateBuilder<(string? Key, char Kind, int Hp)>();
        foreach (Match match in GroupRegex.Matches(raw))
        {
            string? key = ElementKeyFromLetter(match.Groups["element"].Value[0]);
            if (key is null)
            {
                continue;
            }

            char kind = char.ToUpperInvariant(match.Groups["kind"].Value[0]);
            foreach (string hp in match.Groups["hps"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(hp, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    builder.Add((key, kind, value));
                }
            }
        }

        return builder.ToImmutable();
    }

    private static string? ElementKeyOf(ImmutableArray<(string? Key, char Kind, int Hp)> groups, int hp, char kind)
    {
        foreach ((string? key, char groupKind, int groupHp) in groups)
        {
            if (groupKind == kind && groupHp == hp)
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>元素字母 → 配色键；无法识别时返回 <see langword="null"/>（即不上色）。</summary>
    private static string? ElementKeyFromLetter(char letter)
    {
        return char.ToUpperInvariant(letter) switch
        {
            'P' => "pyro",
            'H' => "hydro",
            'E' => "electro",
            'A' => "anemo",
            'D' => "dendro",
            'C' => "cryo",
            'G' => "geo",
            _ => null,
        };
    }
}

/// <summary>单个血量阈值下的产球。</summary>
public sealed class EnemyArchiveEnergyRow
{
    [JsonPropertyName("hp")]
    public int Hp { get; set; }

    [JsonPropertyName("particle")]
    public int Particle { get; set; }

    [JsonPropertyName("orb")]
    public int Orb { get; set; }

    public string HpText => string.Create(CultureInfo.InvariantCulture, $"{Hp}%");

    /// <summary>无掉落时留空（不用「—」占位）。</summary>
    public string ParticleText => Particle > 0 ? Particle.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>无掉落时留空（不用「—」占位）。</summary>
    public string OrbText => Orb > 0 ? Orb.ToString(CultureInfo.InvariantCulture) : string.Empty;
}

/// <summary>产球表格里的一格（界面直接绑定）。</summary>
public sealed class EnemyArchiveEnergyCell
{
    public required string HpText { get; init; }

    public required string ParticleText { get; init; }

    public required string OrbText { get; init; }

    /// <summary>元素微粒的配色键；无色则不上色。</summary>
    public required string? ParticleElementKey { get; init; }

    /// <summary>元素晶球的配色键；无色则不上色。</summary>
    public required string? OrbElementKey { get; init; }
}

/// <summary>一个数值档位。</summary>
public sealed class EnemyArchiveVariant
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("hp")]
    public string? Hp { get; set; }

    [JsonPropertyName("atk")]
    public string? Atk { get; set; }

    public string Display => $"{Name} · Lv.{Level}";
}

/// <summary>抗性（常态 + 若干机制状态）。</summary>
public sealed class EnemyArchiveResistances
{
    [JsonPropertyName("values")]
    public EnemyArchiveResistanceValues? Values { get; set; }

    /// <summary>机制状态下的不同抗性；为空表示该敌人只有一套抗性。</summary>
    [JsonPropertyName("states")]
    public ImmutableArray<EnemyArchiveResistanceState> States { get; set; } = [];

    public bool HasStates => !States.IsDefaultOrEmpty;
}

/// <summary>某个机制状态下的抗性。</summary>
public sealed class EnemyArchiveResistanceState
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>触发条件（作为状态切换的提示文本）。</summary>
    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("values")]
    public EnemyArchiveResistanceValues? Values { get; set; }
}

/// <summary>
/// 八项抗性，值为展示用文本（如 <c>10%</c> / <c>IMM</c>）。
/// </summary>
public sealed class EnemyArchiveResistanceValues
{
    [JsonPropertyName("pyro")]
    public string Pyro { get; set; } = "0%";

    [JsonPropertyName("hydro")]
    public string Hydro { get; set; } = "0%";

    [JsonPropertyName("dendro")]
    public string Dendro { get; set; } = "0%";

    [JsonPropertyName("electro")]
    public string Electro { get; set; } = "0%";

    [JsonPropertyName("anemo")]
    public string Anemo { get; set; } = "0%";

    [JsonPropertyName("cryo")]
    public string Cryo { get; set; } = "0%";

    [JsonPropertyName("geo")]
    public string Geo { get; set; } = "0%";

    [JsonPropertyName("physical")]
    public string Physical { get; set; } = "0%";

    /// <summary>
    /// 展开成可直接绑定的单元格。顺序按图鉴卡实际排布：
    /// <b>火 水 风 雷 草 冰 岩 物</b>（已用 22 张元素生命卡的 IMM 位置验证：风在 3、草在 5）。
    /// </summary>
    public ImmutableArray<EnemyArchiveResistanceCell> ToCells()
    {
        return
        [
            Cell("pyro", "火", Pyro),
            Cell("hydro", "水", Hydro),
            Cell("anemo", "风", Anemo),
            Cell("electro", "雷", Electro),
            Cell("dendro", "草", Dendro),
            Cell("cryo", "冰", Cryo),
            Cell("geo", "岩", Geo),
            Cell("physical", "物", Physical),
        ];
    }

    private static EnemyArchiveResistanceCell Cell(string key, string elementName, string raw)
    {
        string value = string.IsNullOrWhiteSpace(raw) ? "0%" : raw.Trim();
        bool immune = value.Contains("IMM", StringComparison.OrdinalIgnoreCase)
            || value.Contains('免');

        double number = 0;
        if (!immune)
        {
            _ = double.TryParse(value.TrimEnd('%', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        return new()
        {
            ElementKey = key,
            ElementName = elementName,
            Label = key is "physical" ? "物理抗性" : $"{elementName}元素抗性",
            ValueText = immune ? "IMM" : value,
            IsImmune = immune,
            IsZero = !immune && number is 0,
            IsHigh = !immune && number >= 50,
        };
    }
}

/// <summary>抗性单元格（已展开好，供界面直接绑定）。</summary>
public sealed class EnemyArchiveResistanceCell
{
    public required string ElementKey { get; init; }

    public required string ElementName { get; init; }

    public required string Label { get; init; }

    public required string ValueText { get; init; }

    public required bool IsImmune { get; init; }

    public required bool IsZero { get; init; }

    public required bool IsHigh { get; init; }
}

/// <summary>特殊机制。</summary>
public sealed class EnemyArchiveMechanic
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>正文，支持 <c>**加粗**</c> / <c>`专有名词`</c> / <c>$公式$</c> 标记。</summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title);
}

/// <summary>技能。</summary>
public sealed class EnemyArchiveSkill
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>类型标签，如「输出」「控制」「召唤」「天赋」「亡语」。</summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    /// <summary>正文，支持富文本标记。</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>应对 / 躲避提示。</summary>
    [JsonPropertyName("tips")]
    public string? Tips { get; set; }

    /// <summary>相关连招。</summary>
    [JsonPropertyName("combo")]
    public ImmutableArray<string> Combo { get; set; } = [];

    /// <summary>伤害强度 0~5（无则不显示）。</summary>
    [JsonPropertyName("damage")]
    public int Damage { get; set; }

    /// <summary>躲避难度 0~5（无则不显示）。</summary>
    [JsonPropertyName("dodge")]
    public int Dodge { get; set; }

    public bool HasTag => !string.IsNullOrWhiteSpace(Tag);

    public bool HasTips => !string.IsNullOrWhiteSpace(Tips);

    public bool HasCombo => !Combo.IsDefaultOrEmpty;

    /// <summary>已拼接好的连招文本（前缀文案由界面提供）。</summary>
    public string ComboText => Combo.IsDefaultOrEmpty ? string.Empty : string.Join(" → ", Combo);

    public bool HasRating => Damage > 0 || Dodge > 0;
}
