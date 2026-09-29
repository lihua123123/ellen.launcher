// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Abstraction;

public interface ICultivationItemsAccess : INameAccess
{
    ImmutableArray<MaterialId> CultivationItems { get; }
}