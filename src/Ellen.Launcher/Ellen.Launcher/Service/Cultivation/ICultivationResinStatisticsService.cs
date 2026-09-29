// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.Cultivation;

namespace Ellen.Launcher.Service.Cultivation;

public interface ICultivationResinStatisticsService
{
    ValueTask<ResinStatistics> GetResinStatisticsAsync(IEnumerable<StatisticsCultivateItem> statisticsCultivateItems, CancellationToken token);
}