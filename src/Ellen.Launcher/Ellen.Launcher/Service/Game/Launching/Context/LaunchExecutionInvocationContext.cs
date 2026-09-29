// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.Game;

namespace Ellen.Launcher.Service.Game.Launching.Context;

public sealed class LaunchExecutionInvocationContext
{
    public required IViewModelSupportLaunchExecution ViewModel { get; init; }

    public required IServiceProvider ServiceProvider { get; init; }

    public required LaunchOptions LaunchOptions { get; init; }

    public required GameIdentity Identity { get; init; }
}