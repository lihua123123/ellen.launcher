// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Graphics;
using Ellen.Launcher.UI.Windowing;
using Windows.Graphics;

namespace Ellen.Launcher.UI.Xaml.View.Window;

public static class ScaledSizeInt32
{
    public static SizeInt32 CreateForWindow(int width, int height, Microsoft.UI.Xaml.Window window)
    {
        return new SizeInt32(width, height).Scale(window.RasterizationScale);
    }
}