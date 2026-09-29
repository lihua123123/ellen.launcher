// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata;

public sealed class RoleCombatSchedule : IDefaultIdentity<RoleCombatScheduleId>
{
    public required RoleCombatScheduleId Id { get; init; }

    public required DateTimeOffset Begin { get; init; }

    public required DateTimeOffset End { get; init; }

    public required ImmutableArray<ElementType> Elements { get; init; }

    public required ImmutableArray<AvatarId> SpecialAvatars { get; init; }

    public required ImmutableArray<AvatarId> InitialAvatars { get; init; }
}