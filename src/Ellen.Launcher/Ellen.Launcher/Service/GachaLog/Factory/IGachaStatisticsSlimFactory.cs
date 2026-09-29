// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.ViewModel.GachaLog;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.GachaLog.Factory;

public interface IGachaStatisticsSlimFactory
{
    ValueTask<GachaStatisticsSlim> CreateAsync(GachaLogServiceMetadataContext context, ImmutableArray<GachaItem> items, string uid);
}