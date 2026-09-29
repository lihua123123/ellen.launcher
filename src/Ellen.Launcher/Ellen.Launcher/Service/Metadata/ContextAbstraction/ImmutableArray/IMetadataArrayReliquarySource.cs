// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Reliquary;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;

public interface IMetadataArrayReliquarySource
{
    ImmutableArray<Reliquary> Reliquaries { get; set; }
}