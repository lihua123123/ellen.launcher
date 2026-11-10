// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Monster;
using Ellen.Launcher.UI.Xaml.Data;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Ellen.Launcher.Model.EnemyArchive;

/// <summary>
/// 敌人图鉴条目，来源于随应用发布的 <c>Resource/EnemyArchives/MonsterCatalog.json</c>。
/// <para>
/// 与旧的「元数据 <c>Monster.json</c> + 按名字匹配档案」不同，这里是**一份数据到底**：
/// 一条 = 上游 GIEnemyChartXML 的一个 <c>InternalName</c>，名字、分类、头像、基础值、
/// 档案（抗性/技能/护盾/机制）全部内联，不再有名字匹配这一步。
/// </para>
/// <para>
/// 好处是「望乡的孤狼」（地方传奇）与「历经百战的凛狼·极寒冰魄」（幽境危战）是两个独立条目 ——
/// 它们在上游就是两条不同的配置，元数据时代因为同名而被合并成一条，技能与图标也随之错配。
/// </para>
/// </summary>
public sealed partial class MonsterCatalogEntry : IPropertyValuesProvider
{
    /// <summary>上游内码，如 <c>MagBeast_SteppenWolf_Elite_Leyline</c>。全表唯一。</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>显示名，如「历经百战的凛狼·极寒冰魄」。</summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>副标题，如「狂雷刻印的丘丘王者」；源里没有则为空。</summary>
    [JsonPropertyName("subtitle")]
    public string? Subtitle { get; init; }

    /// <summary>目录表里的全名（可能带战役前缀），如「啸卷之役-历经百战的凛狼·极寒冰魄」。</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>首次登场玩法，如「啸卷之役」；没有则为空。</summary>
    [JsonPropertyName("campaign")]
    public string? Campaign { get; init; }

    /// <summary>上游分类键，如 <c>Legends</c> / <c>LeylineChallenge</c>。</summary>
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    /// <summary>分类中文名，如「地方传奇」。</summary>
    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; init; }

    /// <summary>上游层级键，如 <c>Legend</c> / <c>Boss</c>。</summary>
    [JsonPropertyName("tier")]
    public string? Tier { get; init; }

    /// <summary>层级中文名，如「传说」「首领」。</summary>
    [JsonPropertyName("tierName")]
    public string? TierName { get; init; }

    /// <summary>头像文件名（= <see cref="Id"/>），对应 <c>Resource/EnemyArchives/MonsterIcons/{Icon}.png</c>。</summary>
    [JsonPropertyName("icon")]
    public required string Icon { get; set; }

    [JsonPropertyName("baseHp")]
    public double BaseHp { get; init; }

    [JsonPropertyName("hpCurve")]
    public string? HpCurve { get; init; }

    [JsonPropertyName("baseAtk")]
    public double BaseAtk { get; init; }

    [JsonPropertyName("atkCurve")]
    public string? AtkCurve { get; init; }

    [JsonPropertyName("baseDef")]
    public double BaseDef { get; init; }

    [JsonPropertyName("defCurve")]
    public string? DefCurve { get; init; }

    /// <summary>内联档案（抗性 / 技能 / 护盾 / 机制 / 产球）。</summary>
    [JsonPropertyName("archive")]
    public EnemyArchiveEntry? Archive { get; init; }

    /// <summary>详情页大标题：就是这个敌人的名字。</summary>
    public string Title => Name;

    /// <summary>
    /// 标题下的小字：优先用源里的副标题（如「狂雷刻印的丘丘王者」），没有才退到登场玩法。
    /// <para>分类名不重复放在这里 —— 它已经在 <see cref="Affixes"/> 的胶囊里显示过。</para>
    /// </summary>
    public string? Description => !string.IsNullOrWhiteSpace(Subtitle)
        ? Subtitle
        : string.IsNullOrWhiteSpace(Campaign) ? null : Campaign;

    /// <summary>
    /// 列表与详情页的胶囊标签：分类 + 层级（去重）。
    /// <para>
    /// ⚠️ 运行时类型必须是 <see cref="List{T}"/>：详情页用 <c>TokenView</c> 绑这个属性，
    /// 而 <c>TokenView</c> 会对 ItemsSource 的运行时类型反射取 <c>Remove</c> 方法；
    /// <see cref="ImmutableArray{T}"/> 同时有 <c>Remove(T)</c> 与 <c>Remove(T, IEqualityComparer)</c>
    /// 两个重载，反射会抛 <see cref="System.Reflection.AmbiguousMatchException"/> 把整个界面打崩。
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Affixes => this.affixes ??= BuildAffixes();

    private List<string>? affixes;

    private List<string> BuildAffixes()
    {
        List<string> builder = new(2);
        if (!string.IsNullOrWhiteSpace(CategoryName))
        {
            builder.Add(CategoryName);
        }

        if (!string.IsNullOrWhiteSpace(TierName) && TierName != CategoryName)
        {
            builder.Add(TierName);
        }

        return builder;
    }

    /// <summary>上游没有掉落物数据，留空（界面据此隐藏掉落区）。</summary>
    public ImmutableArray<DisplayItem> DropsView => [];

    /// <summary>是否有可显示的头像；<see langword="false"/> 时列表只显示文字。</summary>
    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);

    /// <summary>建好曲线的基础值，供详情页的等级滑条使用。</summary>
    public MonsterBaseValue? BaseValue { get; init; }

    /// <summary>基础值对应的成长曲线（HP / 攻击 / 防御三条）。</summary>
    public TypeValueCollection<FightProperty, GrowCurveType>? GrowCurves { get; init; }

    public bool HasArchiveResistances => Archive?.HasResistances is true;

    public bool HasArchiveMechanics => Archive?.HasMechanics is true;

    public bool HasArchiveShield => Archive?.HasShield is true;

    public bool HasArchiveEnergy => Archive?.HasEnergy is true;

    public bool HasArchiveSkills => Archive?.HasSkills is true;

    /// <summary>
    /// 是否用基础值里的通用抗性垫底。
    /// 抗性严格按上游图鉴来：上游没解析到就不显示抗性区，不再伪造 10%，因此恒为 false。
    /// </summary>
    public bool ShowFallbackResistances => false;

    /// <summary>
    /// 列表项的无障碍 / UI 自动化名称。
    /// 不重写的话 <see cref="ListViewItem"/> 的自动化 peer 会回落到类型的全名。
    /// </summary>
    public override string ToString()
    {
        return Name;
    }
}

/// <summary>
/// 侧边栏里的一个分类（元素生命 / 地方传奇 / 幽境危战 …）。
/// </summary>
public sealed class MonsterCategoryGroup
{
    /// <summary>分类键（上游英文键，用作稳定标识）。</summary>
    public required string Key { get; init; }

    /// <summary>分类中文名。</summary>
    public required string Name { get; init; }

    /// <summary>该分类下的条目数（「全部」为整表数量）。</summary>
    public required int Count { get; init; }

    /// <summary>数量文案，如「126」。</summary>
    public string CountText => Count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>该分类下的敌人（「全部」存放整表，仅供计数，不参与过滤）。</summary>
    public ImmutableArray<MonsterCatalogEntry> Monsters { get; init; } = [];

    /// <summary>是否为「全部」这一项。</summary>
    public bool IsAll => Key.Length == 0;

    /// <summary>分类项的无障碍 / UI 自动化名称（否则会回落到类型全名）。</summary>
    public override string ToString()
    {
        return Name;
    }
}
