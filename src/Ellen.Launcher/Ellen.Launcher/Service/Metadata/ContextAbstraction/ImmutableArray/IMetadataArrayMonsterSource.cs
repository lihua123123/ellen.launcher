// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Monster;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;

public interface IMetadataArrayMonsterSource
{
    ImmutableArray<Monster> Monsters { get; set; }
}