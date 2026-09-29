// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game;

public sealed class LaunchStatus
{
    public LaunchStatus(string description)
    {
        Description = description;
    }

    public string Description { get; }
}