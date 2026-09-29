// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;

namespace Ellen.Launcher.UI.Xaml.Control.Layout;

public sealed class UniformStaggeredItem
{
    public UniformStaggeredItem(int index)
    {
        Index = index;
    }

    public double Top { get; set; }

    public double Height { get; set; }

    public int Index { get; }

    public UIElement? Element { get; set; }
}