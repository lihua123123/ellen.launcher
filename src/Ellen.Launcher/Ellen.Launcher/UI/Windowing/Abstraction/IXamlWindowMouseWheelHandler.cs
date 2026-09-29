// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Input;

namespace Ellen.Launcher.UI.Windowing.Abstraction;

public interface IXamlWindowMouseWheelHandler
{
    void OnMouseWheel(ref readonly PointerPointProperties data);
}