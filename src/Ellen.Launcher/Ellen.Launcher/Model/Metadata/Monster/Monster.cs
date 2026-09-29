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

    public required string Icon { get; init; }

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
}