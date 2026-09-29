// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Service.Game.Launching.Context;
using Windows.System;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public sealed class LaunchExecutionStarwardPlayTimeStatisticsHandler : AbstractLaunchExecutionHandler
{
    public override async ValueTask ExecuteAsync(LaunchExecutionContext context)
    {
        if (context.Process.IsRunning && context.LaunchOptions.UsingStarwardPlayTimeStatistics.Value)
        {
            await LaunchStarwardForPlayTimeStatisticsAsync(context).ConfigureAwait(false);
        }
    }

    private static async ValueTask LaunchStarwardForPlayTimeStatisticsAsync(LaunchExecutionContext context)
    {
        string gameBiz = context.IsOversea ? "hk4e_global" : "hk4e_cn";
        Uri starwardPlayTimeUri = $"starward://playtime/{gameBiz}".ToUri();
        if (await global::Windows.System.Launcher.QueryUriSupportAsync(starwardPlayTimeUri, LaunchQuerySupportType.Uri) is LaunchQuerySupportStatus.Available)
        {
            await global::Windows.System.Launcher.LaunchUriAsync(starwardPlayTimeUri);
        }
    }
}