// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata;

public class IdCount
{
    public required MaterialId Id { get; init; }

    public required uint Count { get; init; }
}