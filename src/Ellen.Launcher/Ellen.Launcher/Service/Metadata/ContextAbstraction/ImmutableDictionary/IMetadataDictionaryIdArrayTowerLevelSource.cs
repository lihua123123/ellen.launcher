// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Tower;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;

public interface IMetadataDictionaryIdArrayTowerLevelSource
{
    ImmutableDictionary<TowerLevelGroupId, ImmutableArray<TowerLevel>> IdArrayTowerLevelMap { get; set; }
}