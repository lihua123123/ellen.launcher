// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.LifeCycle;

public interface IAppActivationActionHandlersAccess
{
    ValueTask HandleLaunchGameActionAsync(string? uid = null);
}