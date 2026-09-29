// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Game.Launching.Handler;

namespace Ellen.Launcher.Service.Game.Launching.Invoker;

public sealed class ConvertOnlyLaunchExecutionInvoker : AbstractLaunchExecutionInvoker
{
    public ConvertOnlyLaunchExecutionInvoker()
    {
        Handlers =
        [
            new LaunchExecutionGameLifeCycleHandler(resume: false),
            new LaunchExecutionChannelOptionsHandler(),
            new LaunchExecutionGameResourceHandler(convertOnly: true),
        ];
    }

    protected override IProcess? CreateProcess(BeforeLaunchExecutionContext beforeContext)
    {
        // Since this invoker is only for conversion, we do not actually need the process.
        return default;
    }
}