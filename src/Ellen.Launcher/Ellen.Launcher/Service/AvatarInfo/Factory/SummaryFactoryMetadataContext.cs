// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Reliquary;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;
using MetadataReliquary = Ellen.Launcher.Model.Metadata.Reliquary.Reliquary;
using MetadataWeapon = Ellen.Launcher.Model.Metadata.Weapon.Weapon;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

public sealed class SummaryFactoryMetadataContext : IMetadataContext,
    IMetadataDictionaryIdAvatarWithPlayersSource,
    IMetadataDictionaryIdWeaponSource,
    IMetadataDictionaryIdReliquaryMainPropertySource,
    IMetadataDictionaryIdReliquarySetSource,
    IMetadataDictionaryIdReliquarySubAffixSource,
    IMetadataDictionaryIdReliquarySource,
    IMetadataArrayReliquaryMainAffixLevelSource,
    IMetadataDictionaryLevelWeaponGrowCurveSource,
    IMetadataDictionaryIdDictionaryLevelWeaponPromoteSource
{
    public ImmutableDictionary<AvatarId, MetadataAvatar> IdAvatarMap { get; set; } = default!;

    public ImmutableDictionary<WeaponId, MetadataWeapon> IdWeaponMap { get; set; } = default!;

    public ImmutableDictionary<ReliquaryMainAffixId, FightProperty> IdReliquaryMainPropertyMap { get; set; } = default!;

    public ImmutableDictionary<ReliquarySubAffixId, ReliquarySubAffix> IdReliquarySubAffixMap { get; set; } = default!;

    public ImmutableDictionary<ReliquarySetId, ReliquarySet> IdReliquarySetMap { get; set; } = default!;

    public ImmutableArray<ReliquaryMainAffixLevel> ReliquaryMainAffixLevels { get; set; } = default!;

    public ImmutableDictionary<ReliquaryId, MetadataReliquary> IdReliquaryMap { get; set; } = default!;

    public ImmutableDictionary<Level, TypeValueCollection<GrowCurveType, float>> LevelDictionaryWeaponGrowCurveMap { get; set; } = default!;

    public ImmutableDictionary<PromoteId, ImmutableDictionary<PromoteLevel, Promote>> IdDictionaryWeaponLevelPromoteMap { get; set; } = default!;
}