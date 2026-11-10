// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.EnemyArchive;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.UI.Xaml.Data;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Monster;

public sealed partial class Monster : IPropertyValuesProvider
{
    public const uint MaxLevel = 110U;

    public required MonsterId Id { get; init; }

    public required MonsterDescribeId DescribeId { get; init; }

    public string? MonsterName { get; init; }

    public string? Name { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// 头像图标名（<c>UI_MonsterIcon_*</c>）。图鉴页会用 GitHub 目录表的
    /// <c>IconPath</c> 覆盖成更准确的子体图标，因此这里不是 <c>init</c>。
    /// <para>
    /// 源里取不到图时会被置空（见 <c>MonsterIconUnavailable.json</c>），
    /// 界面据此换成纯文字占位而不是破图。
    /// </para>
    /// </summary>
    public required string Icon { get; set; }

    /// <summary>是否有可显示的头像；<see langword="false"/> 时列表只显示文字。</summary>
    [JsonIgnore]
    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);

    public required MonsterType Type { get; init; }

    public required Arkhe Arkhe { get; init; }

    public List<string>? Affixes { get; init; }

    public ImmutableArray<MaterialId> Drops { get; init; }

    public MonsterBaseValue? BaseValue { get; init; }

    public TypeValueCollection<FightProperty, GrowCurveType>? GrowCurves { get; init; }

    [JsonIgnore]
    public ImmutableArray<DisplayItem>? DropsView { get; set; }

    /// <summary>
    /// 社区整理的档案（准确抗性 / 技能 / 机制），由敌人图鉴页按名称匹配后填入；未收录时为 <see langword="null"/>。
    /// </summary>
    [JsonIgnore]
    public EnemyArchiveEntry? Archive { get; set; }

    /// <summary>
    /// 档案里是否有抗性数据（有则应改用档案的值，元数据的 <c>*SubHurt</c> 只是通用占位）。
    /// </summary>
    [JsonIgnore]
    public bool HasArchiveResistances => Archive?.HasResistances is true;

    // 档案可能为 null（严格匹配后有一批怪物没有档案）。XAML 直接绑
    // Archive.HasXxx 时，路径失败会让 Visibility 保持默认 Visible，
    // 页面上会多出一排空标题；所以统一走这些空安全属性。

    [JsonIgnore]
    public bool HasArchiveMechanics => Archive?.HasMechanics is true;

    [JsonIgnore]
    public bool HasArchiveShield => Archive?.HasShield is true;

    [JsonIgnore]
    public bool HasArchiveEnergy => Archive?.HasEnergy is true;

    [JsonIgnore]
    public bool HasArchiveSkills => Archive?.HasSkills is true;
}