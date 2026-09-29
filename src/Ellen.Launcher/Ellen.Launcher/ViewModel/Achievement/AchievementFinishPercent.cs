// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.ViewModel.Achievement;

public static class AchievementFinishPercent
{
    public static void Update(AchievementViewModel viewModel)
    {
        int totalFinished = 0;
        int totalCount = 0;

        if (viewModel.Achievements is not { } achievements)
        {
            return;
        }

        if (viewModel.AchievementGoals is not { } achievementGoals)
        {
            return;
        }

        Dictionary<AchievementGoalId, AchievementGoalStatistics> counter = achievementGoals.Source.ToDictionary(x => x.Id, AchievementGoalStatistics.Create);

        foreach (AchievementView achievementView in achievements.Source)
        {
            ref AchievementGoalStatistics goalStats = ref CollectionsMarshal.GetValueRefOrNullRef(counter, achievementView.Inner.Goal);
            Debug.Assert(!Unsafe.IsNullRef(in goalStats));

            goalStats.TotalCount += 1;
            goalStats.TotalItem201Count += achievementView.Inner.FinishReward.Count;
            totalCount += 1;
            if (achievementView.IsChecked)
            {
                goalStats.Finished += 1;
                goalStats.Item201Finished += achievementView.Inner.FinishReward.Count;
                totalFinished += 1;
            }
        }

        foreach (AchievementGoalStatistics statistics in counter.Values)
        {
            statistics.AchievementGoal.UpdateFinishDescriptionAndPercent(statistics);
        }

        viewModel.FinishDescription = AchievementStatistics.Format(totalFinished, totalCount, out _);
    }
}