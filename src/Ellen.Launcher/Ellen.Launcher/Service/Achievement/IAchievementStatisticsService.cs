// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.Achievement;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Achievement;

public interface IAchievementStatisticsService
{
    ValueTask<ImmutableArray<AchievementStatistics>> GetAchievementStatisticsAsync(AchievementServiceMetadataContext context, CancellationToken token = default);
}