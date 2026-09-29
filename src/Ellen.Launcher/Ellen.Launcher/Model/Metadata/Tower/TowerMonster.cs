// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Tower;

public sealed class TowerMonster
{
    public required MonsterDescribeId Id { get; init; }

    public required uint Count { get; init; }

    public bool AttackMonolith { get; init; }

    public ImmutableArray<NameDescription> Affixes { get; init; }
}