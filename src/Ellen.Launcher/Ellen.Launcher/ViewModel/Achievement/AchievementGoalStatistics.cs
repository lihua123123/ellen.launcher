// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using BindingAchievementGoal = Ellen.Launcher.ViewModel.Achievement.AchievementGoalView;

namespace Ellen.Launcher.ViewModel.Achievement;

public sealed class AchievementGoalStatistics
{
    private AchievementGoalStatistics(BindingAchievementGoal goal)
    {
        AchievementGoal = goal;
    }

    public BindingAchievementGoal AchievementGoal { get; }

    public int Finished { get; set; }

    public uint Item201Finished { get; set; }

    public uint TotalItem201Count { get; set; }

    public int TotalCount { get; set; }

    public static AchievementGoalStatistics Create(BindingAchievementGoal goal)
    {
        return new(goal);
    }
}