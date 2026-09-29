// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata;

public sealed class HardChallengeSchedule : IDefaultIdentity<HardChallengeScheduleId>
{
    public required HardChallengeScheduleId Id { get; init; }

    public required DateTimeOffset Begin { get; init; }

    public required DateTimeOffset End { get; init; }

    public required string Name { get; init; }
}