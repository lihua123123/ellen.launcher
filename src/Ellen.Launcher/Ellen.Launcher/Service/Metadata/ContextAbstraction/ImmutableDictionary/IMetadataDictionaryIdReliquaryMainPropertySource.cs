// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;

public interface IMetadataDictionaryIdReliquaryMainPropertySource
{
    ImmutableDictionary<ReliquaryMainAffixId, FightProperty> IdReliquaryMainPropertyMap { get; set; }
}