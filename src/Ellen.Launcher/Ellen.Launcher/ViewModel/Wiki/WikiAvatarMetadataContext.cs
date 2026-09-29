// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Wiki;

public sealed class WikiAvatarMetadataContext : IMetadataContext,
    IMetadataDictionaryLevelAvatarGrowCurveSource,
    IMetadataDictionaryIdDictionaryLevelAvatarPromoteSource,
    IMetadataDictionaryIdMaterialSource,
    IMetadataDictionaryIdHyperLinkNameSource,
    IMetadataArrayAvatarSource
{
    public ImmutableDictionary<Level, TypeValueCollection<GrowCurveType, float>> LevelDictionaryAvatarGrowCurveMap { get; set; } = default!;

    public ImmutableDictionary<PromoteId, ImmutableDictionary<PromoteLevel, Promote>> IdDictionaryAvatarLevelPromoteMap { get; set; } = default!;

    public ImmutableDictionary<MaterialId, Material> IdMaterialMap { get; set; } = default!;

    public ImmutableDictionary<HyperLinkNameId, HyperLinkName> IdHyperLinkNameMap { get; set; } = default!;

    public ImmutableArray<Avatar> Avatars { get; set; }
}