// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata;

public sealed class HyperLinkName : IDefaultIdentity<HyperLinkNameId>
{
    public required HyperLinkNameId Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }
}