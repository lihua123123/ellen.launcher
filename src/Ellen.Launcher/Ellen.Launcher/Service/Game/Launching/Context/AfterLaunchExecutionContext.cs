// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.Launching.Context;

public sealed class AfterLaunchExecutionContext
{
    public required IServiceProvider ServiceProvider { get; init; }

    public required ITaskContext TaskContext { get; init; }

    public required LaunchOptions LaunchOptions { get; init; }

    public required GameIdentity Identity { get; init; }
}