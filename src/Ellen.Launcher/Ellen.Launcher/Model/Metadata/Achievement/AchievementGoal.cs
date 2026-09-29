// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Achievement;

public sealed class AchievementGoal
{
    public required AchievementGoalId Id { get; init; }

    public required uint Order { get; init; }

    public required string Name { get; init; }

    public Reward? FinishReward { get; init; }

    public required string Icon { get; init; }
}