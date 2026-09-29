// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.LifeCycle.InterProcess.Yae;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Game.Launching.Handler;
using Ellen.Launcher.Service.Yae.Achievement;

namespace Ellen.Launcher.Service.Game.Launching.Invoker;

public sealed class YaeLaunchExecutionInvoker : AbstractLaunchExecutionInvoker
{
    public YaeLaunchExecutionInvoker(TargetNativeConfiguration config, YaeDataArrayReceiver receiver)
    {
        Handlers =
        [
            new LaunchExecutionGameLifeCycleHandler(resume: false),
            new LaunchExecutionChannelOptionsHandler(),
            new LaunchExecutionGameResourceHandler(false),
            new LaunchExecutionGameIdentityHandler(),
            new LaunchExecutionGameProcessStartHandler(),
            new LaunchExecutionYaeNamedPipeHandler(config, receiver),
        ];
    }

    protected override IProcess? CreateProcess(BeforeLaunchExecutionContext beforeContext)
    {
        return GameProcessFactory.CreateForEmbeddedYae(beforeContext);
    }
}