// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Game.Launching.Context;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public interface ILaunchExecutionHandler
{
    ValueTask BeforeAsync(BeforeLaunchExecutionContext context);

    ValueTask ExecuteAsync(LaunchExecutionContext context);

    ValueTask AfterAsync(AfterLaunchExecutionContext context);
}