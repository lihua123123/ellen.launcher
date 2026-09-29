// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata;

public sealed class Combine
{
    public required CombineId Id { get; init; }

    public required uint Type { get; init; }

    public required uint SubType { get; init; }

    public required RecipeType RecipeType { get; init; }

    public required uint Cost { get; init; }

    public required IdCount Result { get; init; }

    public required ImmutableArray<IdCount> Materials { get; init; }

    public string EffectDescription { get; init; } = default!;
}