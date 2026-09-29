// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.Model.Metadata.Abstraction;

public interface IStatisticsItemConvertible
{
    StatisticsItem ToStatisticsItem(int count);
}