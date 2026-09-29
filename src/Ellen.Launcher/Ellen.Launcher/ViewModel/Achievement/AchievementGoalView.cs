// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Metadata.Achievement;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.UI.Xaml.Data;

namespace Ellen.Launcher.ViewModel.Achievement;

public sealed partial class AchievementGoalView : ObservableObject,
    INameIcon<Uri>,
    IPropertyValuesProvider
{
    private AchievementGoalView(AchievementGoal goal)
    {
        Id = goal.Id;
        Order = goal.Order;
        Name = goal.Name;
        Icon = AchievementIconConverter.IconNameToUri(goal.Icon);
    }

    public AchievementGoalId Id { get; }

    public uint Order { get; }

    public string Name { get; }

    public Uri Icon { get; }

    [ObservableProperty]
    public partial double FinishPercent { get; private set; }

    [ObservableProperty]
    public partial string? FinishDescription { get; private set; }

    [ObservableProperty]
    public partial string? Item201Description { get; private set; }

    public static AchievementGoalView Create(AchievementGoal source)
    {
        return new(source);
    }

    public void UpdateFinishDescriptionAndPercent(AchievementGoalStatistics statistics)
    {
        FinishDescription = AchievementStatistics.Format(statistics.Finished, statistics.TotalCount, out double finishPercent);
        Item201Description = AchievementStatistics.FormatItem201(statistics.Item201Finished, statistics.TotalItem201Count);
        FinishPercent = finishPercent;
    }
}