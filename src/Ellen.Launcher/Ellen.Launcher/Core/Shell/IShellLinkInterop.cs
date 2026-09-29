// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.Shell;

public interface IShellLinkInterop
{
    bool TryCreateDesktopShortcut();

    bool TryCreateGameLaunchShortcut();
}