// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class TraceEffect
{
    public required MaterialId Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Icon { get; init; }
}