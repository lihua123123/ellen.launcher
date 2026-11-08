// C2 契约 DTO：/api/enemies/index.json + items/<id>.json（冻结为 enemy-item-v1）。
// 28 个顶层字段（21 全量 + 7 可选）+ 10 个 P4 扩展槽位。
// 与 Contracts/LauncherSnapshotDtos.cs 严格分离，禁止互相引用或直接绑定 UI。

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ellen.Launcher.Domain.WikiSource.Contracts;

public sealed class EnemyIndexDocumentDto
{
    [JsonPropertyName("categories")]
    public List<EnemyCategoryDto>? Categories { get; set; }

    [JsonPropertyName("enemies")]
    public List<EnemyIndexEntryDto> Enemies { get; set; } = [];

    /// <summary>源 index 自带 5 条 × 110 级曲线；当前 dist API 索引是纯数组，此项为 null（G1）。</summary>
    [JsonPropertyName("curves")]
    public Dictionary<string, List<double>>? Curves { get; set; }
}

public sealed class EnemyCategoryDto
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
}

public sealed class EnemyIndexEntryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("nameEn")]
    public string? NameEn { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("categoryLabel")]
    public string CategoryLabel { get; set; } = string.Empty;

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("hasLeyline")]
    public bool HasLeyline { get; set; }

    [JsonPropertyName("leyName")]
    public string? LeyName { get; set; }

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("stateCount")]
    public int StateCount { get; set; }
}

/// <summary>enemy-item-v1：28 字段 + 扩展槽位。字段增减必须走 ADR 变更。</summary>
public sealed class EnemyItemDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("nameEn")] public string? NameEn { get; set; }
    [JsonPropertyName("subtitle")] public string? Subtitle { get; set; }
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("categoryLabel")] public string CategoryLabel { get; set; } = string.Empty;
    [JsonPropertyName("tier")] public string? Tier { get; set; }
    [JsonPropertyName("defaultLevel")] public JsonElement? DefaultLevel { get; set; }
    [JsonPropertyName("baseStats")] public EnemyBaseStatsDto? BaseStats { get; set; }
    [JsonPropertyName("resistances")] public List<EnemyResistanceStateDto> Resistances { get; set; } = [];
    [JsonPropertyName("elementMastery")] public JsonElement? ElementMastery { get; set; }
    [JsonPropertyName("multiplayerGroup")] public JsonElement? MultiplayerGroup { get; set; }
    [JsonPropertyName("weight")] public JsonElement? Weight { get; set; }
    [JsonPropertyName("skills")] public List<EnemySkillDto> Skills { get; set; } = [];
    [JsonPropertyName("notes")] public List<EnemyNoteDto> Notes { get; set; } = [];
    [JsonPropertyName("shields")] public List<EnemyShieldDto> Shields { get; set; } = [];
    [JsonPropertyName("chargebar")] public List<EnemyChargeBarDto> ChargeBar { get; set; } = [];
    [JsonPropertyName("energy")] public EnemyEnergyDto? Energy { get; set; }
    [JsonPropertyName("levelRows")] public List<EnemyLevelRowDto> LevelRows { get; set; } = [];
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];

    [JsonPropertyName("leyline")] public EnemyLeylineDto? Leyline { get; set; }
    [JsonPropertyName("images")] public List<string> Images { get; set; } = [];
    [JsonPropertyName("leylineImages")] public List<string> LeylineImages { get; set; } = [];
    [JsonPropertyName("aliases")] public List<string> Aliases { get; set; } = [];
    [JsonPropertyName("states")] public List<EnemyStateDto>? States { get; set; }
    [JsonPropertyName("defaultState")] public string? DefaultState { get; set; }
    [JsonPropertyName("statVariants")] public List<EnemyStatVariantDto>? StatVariants { get; set; }

    // ---- P4 扩展槽位：当前 753 条数据里都不存在，P1 保留 optional，禁止删字段 ----
    [JsonPropertyName("evidence")] public string? Evidence { get; set; }
    [JsonPropertyName("mechanics")] public List<EnemyMechanicDto>? Mechanics { get; set; }
    [JsonPropertyName("variants")] public List<EnemyVariantDto>? Variants { get; set; }
    [JsonPropertyName("campaign")] public string? Campaign { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
}

public sealed class EnemyBaseStatsDto
{
    [JsonPropertyName("hp")] public EnemyStatDto? Hp { get; set; }
    [JsonPropertyName("atk")] public EnemyStatDto? Atk { get; set; }
    [JsonPropertyName("def")] public EnemyStatDto? Def { get; set; }
}

/// <summary>40 条数据这里是 {}，Value/Curve 均为 null → 领域模型记「未知」，禁止填 0。</summary>
public sealed class EnemyStatDto
{
    [JsonPropertyName("Value")] public double? Value { get; set; }
    [JsonPropertyName("Curve")] public string? Curve { get; set; }
}

public sealed class EnemyResistanceStateDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("diffNote")] public string? DiffNote { get; set; }
    [JsonPropertyName("pyro")] public JsonElement? Pyro { get; set; }
    [JsonPropertyName("hydro")] public JsonElement? Hydro { get; set; }
    [JsonPropertyName("anemo")] public JsonElement? Anemo { get; set; }
    [JsonPropertyName("electro")] public JsonElement? Electro { get; set; }
    [JsonPropertyName("dendro")] public JsonElement? Dendro { get; set; }
    [JsonPropertyName("cryo")] public JsonElement? Cryo { get; set; }
    [JsonPropertyName("geo")] public JsonElement? Geo { get; set; }
    [JsonPropertyName("physical")] public JsonElement? Physical { get; set; }

    // 扩展槽位
    [JsonPropertyName("evidence")] public string? Evidence { get; set; }
    [JsonPropertyName("condition")] public string? Condition { get; set; }
}

public sealed class EnemySkillDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("icon")] public string? Icon { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }

    // 扩展槽位
    [JsonPropertyName("tips")] public string? Tips { get; set; }
    [JsonPropertyName("combo")] public List<string>? Combo { get; set; }
    [JsonPropertyName("damage")] public int? Damage { get; set; }
    [JsonPropertyName("dodge")] public int? Dodge { get; set; }
}

public sealed class EnemyNoteDto
{
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("desc")] public string? Desc { get; set; }
}

public sealed class EnemyShieldDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("elementType")] public string? ElementType { get; set; }

    /// <summary>上游混合类型：208 处 string（含空串）、47 处 number。P1 冻结清单误登记为 string，
    /// 导致 40 条 item 解析失败（713/753）。P2 修正为 JsonElement?，由 adapter 归一，见 ADR R2。</summary>
    [JsonPropertyName("duration")] public JsonElement? Duration { get; set; }
}

/// <summary>源里字段名是 PascalCase；读取时开启 PropertyNameCaseInsensitive。</summary>
public sealed class EnemyChargeBarDto
{
    [JsonPropertyName("Name")] public string? Name { get; set; }
    [JsonPropertyName("Description")] public string? Description { get; set; }
}

public sealed class EnemyEnergyDto
{
    [JsonPropertyName("element")] public string? Element { get; set; }
    [JsonPropertyName("elementLabel")] public string? ElementLabel { get; set; }
    [JsonPropertyName("raw")] public string? Raw { get; set; }
    [JsonPropertyName("rows")] public List<EnemyEnergyRowDto> Rows { get; set; } = [];
}

public sealed class EnemyEnergyRowDto
{
    [JsonPropertyName("hp")] public int Hp { get; set; }
    [JsonPropertyName("particle")] public int Particle { get; set; }
    [JsonPropertyName("orb")] public int Orb { get; set; }
}

public sealed class EnemyLevelRowDto
{
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("hp")] public double? Hp { get; set; }
    [JsonPropertyName("atk")] public double? Atk { get; set; }
    [JsonPropertyName("weight")] public double? Weight { get; set; }
    [JsonPropertyName("tenacity")] public double? Tenacity { get; set; }
}

public sealed class EnemyLeylineDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("nameEn")] public string? NameEn { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("categoryLabel")] public string? CategoryLabel { get; set; }
    [JsonPropertyName("portrait")] public string? Portrait { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class EnemyStateDto
{
    [JsonPropertyName("key")] public string? Key { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class EnemyStatVariantDto
{
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("level")] public int? Level { get; set; }
    [JsonPropertyName("hp")] public double? Hp { get; set; }
    [JsonPropertyName("atk")] public double? Atk { get; set; }
    [JsonPropertyName("weight")] public double? Weight { get; set; }
    [JsonPropertyName("tenacity")] public double? Tenacity { get; set; }
}

public sealed class EnemyMechanicDto
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public sealed class EnemyVariantDto
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("level")] public int? Level { get; set; }
    [JsonPropertyName("hp")] public string? Hp { get; set; }
    [JsonPropertyName("atk")] public string? Atk { get; set; }
}
