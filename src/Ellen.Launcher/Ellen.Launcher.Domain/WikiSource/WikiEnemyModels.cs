// P1 领域模型：adapter 的输出，P2 的 ViewModel/XAML 只认这一层。
// 网页版丢失的旧字段全部保留 optional 槽位（当前为 null/空），P4 补齐只改 adapter 填充。

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class WikiEnemy
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? NameEn { get; init; }
    public string? Subtitle { get; init; }
    public string? Description { get; init; }
    public string CategoryKey { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string? Tier { get; init; }

    /// <summary>本地展示映射（Common→普通 等），不是上游数据，不参与契约冻结。</summary>
    public string? TierName { get; init; }

    public int? DefaultLevel { get; init; }

    /// <summary>null = 上游没有数值（40 条），UI 留空，禁止填 0。</summary>
    public WikiBaseValue? BaseValue { get; init; }

    public bool BaseValueUnknown => BaseValue is null;

    /// <summary>
    /// 列表头像文件名（源里的 <c>icon</c>，如 avatar.webp）。
    /// 为空 = 上游没有头像 → 列表留空，**不许拿一图流（reference*.png）顶替**。
    /// </summary>
    public string? IconFile { get; init; }

    public IReadOnlyList<WikiResistanceState> ResistanceStates { get; init; } = [];
    public IReadOnlyList<WikiSkill> Skills { get; init; } = [];
    public IReadOnlyList<WikiNote> Notes { get; init; } = [];
    public IReadOnlyList<WikiShield> Shields { get; init; } = [];
    public IReadOnlyList<WikiChargeBar> ChargeBars { get; init; } = [];
    public WikiEnergy? Energy { get; init; }
    public IReadOnlyList<WikiLevelRow> LevelRows { get; init; } = [];
    public IReadOnlyList<WikiImageRef> Images { get; init; } = [];

    /// <summary>幽境形态的一图流文件名（leyline.png / leyline-2.png…），本体 images 另算。</summary>
    public IReadOnlyList<string> LeylineImages { get; init; } = [];
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public IReadOnlyList<string> Tags { get; init; } = [];
    public WikiLeyline? Leyline { get; init; }
    public IReadOnlyList<WikiEnemyState> States { get; init; } = [];
    public string? DefaultState { get; init; }

    public WikiSourceStamp Stamp { get; init; } = new();

    // ---- P4 扩展槽位：P1 恒为 null/空，禁止删字段 ----
    public string? Evidence { get; init; }
    public IReadOnlyList<WikiMechanic>? Mechanics { get; init; }
    public IReadOnlyList<WikiVariant>? Variants { get; init; }
    public string? Campaign { get; init; }
    public string? DisplayName { get; init; }
}

public sealed class WikiBaseValue
{
    public double Hp { get; init; }
    public double Atk { get; init; }
    public double Def { get; init; }
    public string? HpCurve { get; init; }
    public string? AtkCurve { get; init; }
    public string? DefCurve { get; init; }
}

public sealed class WikiResistanceState
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<WikiResistanceCell> Cells { get; init; } = [];
    public bool IsDefault { get; init; }
    public string? DiffNote { get; init; }
    public string? Evidence { get; init; }
    public string? Condition { get; init; }
}

public sealed class WikiResistanceCell
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string ValueText { get; init; } = string.Empty;
    public bool IsImmune { get; init; }
    public bool IsHigh { get; init; }
}

public sealed class WikiSkill
{
    public string Name { get; init; } = string.Empty;
    public string? Tag { get; init; }
    public IReadOnlyList<WikiTextSpan> Description { get; init; } = [];

    /// <summary>技能说明的纯文本（spans 已由 <see cref="WikiRichText"/> 去掉标记），供图鉴卡片直接显示。</summary>
    public string DescriptionText => string.Concat(Description.Select(static span => span.Text));
    public WikiImageRef? Icon { get; init; }

    // 扩展槽位
    public string? Tips { get; init; }
    public IReadOnlyList<string>? Combo { get; init; }
    public int? Damage { get; init; }
    public int? Dodge { get; init; }
}

public sealed class WikiNote
{
    public string? Text { get; init; }
    public IReadOnlyList<WikiTextSpan> Description { get; init; } = [];
}

public sealed class WikiShield
{
    public string? Name { get; init; }
    public IReadOnlyList<WikiTextSpan> Description { get; init; } = [];
    public string? ElementType { get; init; }
    public string? Duration { get; init; }
}

public sealed class WikiChargeBar
{
    public string? Name { get; init; }
    public IReadOnlyList<WikiTextSpan> Description { get; init; } = [];
}

public sealed class WikiEnergy
{
    public string? Element { get; init; }
    public string? Raw { get; init; }
    public IReadOnlyList<WikiEnergyRow> Rows { get; init; } = [];
}

public sealed class WikiEnergyRow
{
    public int Hp { get; init; }
    public int Particle { get; init; }
    public int Orb { get; init; }
}

public sealed class WikiLevelRow
{
    public string? Label { get; init; }
    public int Level { get; init; }
    public double? Hp { get; init; }
    public double? Atk { get; init; }
    public double? Weight { get; init; }
    public double? Tenacity { get; init; }
}

/// <summary>P1 在线 URL + 内容寻址缓存键；P4 拿到 assets manifest/hash 后改成本地路径。</summary>
public sealed class WikiImageRef
{
    public string EnemyId { get; init; } = string.Empty;
    public string File { get; init; } = string.Empty;
    public string Uri { get; init; } = string.Empty;
    public string CacheKey { get; init; } = string.Empty;
    public bool HashAvailable { get; init; }
    public string? ContentHash { get; init; }
}

public sealed class WikiLeyline
{
    public string? Name { get; init; }
    public string? NameEn { get; init; }
    public string? CategoryLabel { get; init; }
    public WikiImageRef? Portrait { get; init; }
    public IReadOnlyList<WikiResistanceState> Resistances { get; init; } = [];
    public IReadOnlyList<WikiLeylineDifficulty> Difficulties { get; init; } = [];
    public IReadOnlyList<WikiSkill> Skills { get; init; } = [];
    public IReadOnlyList<WikiNote> Notes { get; init; } = [];
}

/// <summary>幽境难度档（困难 / 险恶 / …）：只带展示需要的三列，不做换算。</summary>
public sealed class WikiLeylineDifficulty
{
    public string? Key { get; init; }
    public string? Name { get; init; }
    public int? Level { get; init; }
    public long? Hp { get; init; }
    public long? Atk { get; init; }
    public double? Multiplier { get; init; }
    public string? Advantage { get; init; }
}

public sealed class WikiEnemyState
{
    public string? Key { get; init; }
    public string? Label { get; init; }
    public string? Name { get; init; }
}

public sealed class WikiMechanic
{
    public string? Title { get; init; }
    public IReadOnlyList<WikiTextSpan> Text { get; init; } = [];
}

public sealed class WikiVariant
{
    public string? Name { get; init; }
    public int? Level { get; init; }
    public string? Hp { get; init; }
    public string? Atk { get; init; }
}
