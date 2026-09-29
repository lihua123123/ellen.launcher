// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.UI.Windowing.Abstraction;

public interface IXamlWindowClosedHandler
{
    void OnWindowClosing(out bool cancel);

    void OnWindowClosed();
}