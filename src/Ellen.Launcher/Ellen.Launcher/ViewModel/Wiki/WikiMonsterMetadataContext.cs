// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Monster;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Wiki;

public sealed class WikiMonsterMetadataContext : IMetadataContext,
    IMetadataDictionaryLevelMonsterGrowCurveSource,
    IMetadataArrayMonsterSource,
    IMetadataDictionaryIdDisplayItemAndMaterialSource
{
    public ImmutableDictionary<Level, TypeValueCollection<GrowCurveType, float>> LevelDictionaryMonsterGrowCurveMap { get; set; } = default!;

    public ImmutableArray<Monster> Monsters { get; set; } = default!;

    public ImmutableDictionary<MaterialId, DisplayItem> IdDisplayItemAndMaterialMap { get; set; } = default!;
}