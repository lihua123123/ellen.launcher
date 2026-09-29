// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class CookBonus
{
    public required MaterialId OriginItemId { get; init; }

    public required MaterialId ItemId { get; init; }

    public required ImmutableArray<MaterialId> InputList { get; init; }
}