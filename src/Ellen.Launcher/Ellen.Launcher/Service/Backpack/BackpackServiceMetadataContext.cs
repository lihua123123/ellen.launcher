// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Reliquary;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Backpack;

public sealed class BackpackServiceMetadataContext : IMetadataContext,
    IMetadataDictionaryIdDisplayItemAndMaterialSource,
    IMetadataDictionaryIdMaterialSource,
    IMetadataDictionaryIdWeaponSource,
    IMetadataDictionaryIdReliquarySource,
    IMetadataDictionaryIdReliquarySetSource,
    IMetadataDictionaryIdReliquaryMainPropertySource,
    IMetadataDictionaryIdReliquarySubAffixSource,
    IMetadataArrayReliquaryMainAffixLevelSource,
    IMetadataArrayCookRecipeSource,
    IMetadataDictionaryIdCookRecipeSource
{
    public ImmutableDictionary<MaterialId, DisplayItem> IdDisplayItemAndMaterialMap { get; set; } = default!;

    public ImmutableDictionary<MaterialId, Material> IdMaterialMap { get; set; } = default!;

    public ImmutableDictionary<WeaponId, Weapon> IdWeaponMap { get; set; } = default!;

    public ImmutableDictionary<ReliquaryId, Reliquary> IdReliquaryMap { get; set; } = default!;

    public ImmutableDictionary<ReliquarySetId, ReliquarySet> IdReliquarySetMap { get; set; } = default!;

    public ImmutableDictionary<ReliquaryMainAffixId, FightProperty> IdReliquaryMainPropertyMap { get; set; } = default!;

    public ImmutableDictionary<ReliquarySubAffixId, ReliquarySubAffix> IdReliquarySubAffixMap { get; set; } = default!;

    public ImmutableArray<ReliquaryMainAffixLevel> ReliquaryMainAffixLevels { get; set; }

    public ImmutableArray<CookRecipe> CookRecipes { get; set; }

    public ImmutableDictionary<CookRecipeId, CookRecipe> IdCookRecipeMap { get; set; } = default!;
}
