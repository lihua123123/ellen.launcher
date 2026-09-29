// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Game.Launching.Handler;

namespace Ellen.Launcher.Service.Game.Launching.Invoker;

public sealed class DefaultLaunchExecutionInvoker : AbstractLaunchExecutionInvoker
{
    public DefaultLaunchExecutionInvoker()
    {
        Handlers =
        [
            new LaunchExecutionGameLifeCycleHandler(resume: false),
            new LaunchExecutionChannelOptionsHandler(),
            new LaunchExecutionGameResourceHandler(convertOnly: false),
            new LaunchExecutionGameIdentityHandler(),
            new LaunchExecutionWindowsHDRHandler(),
            new LaunchExecutionGameProcessStartHandler(),
            new LaunchExecutionStarwardPlayTimeStatisticsHandler(),
            new LaunchExecutionBetterGenshinImpactAutomationHandler(),
            new LaunchExecutionCloseWindowHandler(),
        ];
    }
}