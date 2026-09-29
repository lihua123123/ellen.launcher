// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Navigation;

public interface INavigationCompletionSource
{
    [SuppressMessage("", "SH003")]
    Task WaitForCompletionAsync();
}