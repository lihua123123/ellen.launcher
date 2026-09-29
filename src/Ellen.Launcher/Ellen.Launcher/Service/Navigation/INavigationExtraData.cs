// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Navigation;

public interface INavigationExtraData : INavigationCompletionSource
{
    object? Data { get; set; }

    void NotifyNavigationCompleted();

    void NotifyNavigationException(Exception exception);
}