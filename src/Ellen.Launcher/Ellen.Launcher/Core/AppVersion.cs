// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core;

internal static class AppVersion
{
    public static Version CurrentVersion { get; } = typeof(AppVersion).Assembly.GetName().Version ?? new Version(0, 0, 0, 0);
}
