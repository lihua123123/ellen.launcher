// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Game.Launching.Handler;

namespace Ellen.Launcher.Service.Game.Launching.Invoker;

public sealed class ResumeLaunchExecutionInvoker : AbstractLaunchExecutionInvoker
{
    public ResumeLaunchExecutionInvoker()
    {
        Handlers =
        [
            new LaunchExecutionGameLifeCycleHandler(resume: true),
        ];
    }

    protected override IProcess? CreateProcess(BeforeLaunchExecutionContext context)
    {
        context.TryGetOption(LaunchExecutionOptionsKey.RunningProcess, out IProcess? process);
        return process;
    }
}