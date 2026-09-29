// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Monster;
using Ellen.Launcher.Model.Metadata.Tower;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.SpiralAbyss;

public sealed class SpiralAbyssMetadataContext : IMetadataContext,
    IMetadataDictionaryIdTowerScheduleSource,
    IMetadataDictionaryIdTowerFloorSource,
    IMetadataDictionaryIdArrayTowerLevelSource,
    IMetadataDictionaryIdMonsterSource,
    IMetadataDictionaryIdAvatarWithPlayersSource
{
    public ImmutableDictionary<TowerScheduleId, TowerSchedule> IdTowerScheduleMap { get; set; } = default!;

    public ImmutableDictionary<TowerFloorId, TowerFloor> IdTowerFloorMap { get; set; } = default!;

    public ImmutableDictionary<TowerLevelGroupId, ImmutableArray<TowerLevel>> IdArrayTowerLevelMap { get; set; } = default!;

    public ImmutableDictionary<MonsterDescribeId, Monster> IdMonsterMap { get; set; } = default!;

    public ImmutableDictionary<AvatarId, Avatar> IdAvatarMap { get; set; } = default!;
}