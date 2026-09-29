// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Tower;

public sealed class TowerFloor : IDefaultIdentity<TowerFloorId>
{
    public required TowerFloorId Id { get; init; }

    public required uint Index { get; init; }

    public required TowerLevelGroupId LevelGroupId { get; init; }

    public required string Background { get; init; }

    public required ImmutableArray<string> Descriptions { get; init; }

    public ImmutableArray<string> FirstDescriptions { get; init; }

    public ImmutableArray<string> SecondDescriptions { get; init; }
}