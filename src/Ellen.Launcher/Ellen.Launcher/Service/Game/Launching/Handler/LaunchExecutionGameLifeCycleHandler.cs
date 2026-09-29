// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Service.Game.Launching.Context;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public sealed class LaunchExecutionGameLifeCycleHandler : AbstractLaunchExecutionHandler
{
    private readonly bool resume;

    public LaunchExecutionGameLifeCycleHandler(bool resume)
    {
        this.resume = resume;
    }

    public override async ValueTask BeforeAsync(BeforeLaunchExecutionContext context)
    {
        if (resume)
        {
            if (await GameLifeCycle.TryGetRunningGameProcessAsync(context.TaskContext).ConfigureAwait(false) is (true, { } gameProcess))
            {
                context.SetOption(LaunchExecutionOptionsKey.RunningProcess, gameProcess);
            }
        }
        else
        {
            if (await GameLifeCycle.IsGameRunningAsync(context.TaskContext).ConfigureAwait(false))
            {
                EllenException.Throw(SH.ServiceGameLaunchExecutionGameIsRunning);
            }
        }
    }
}