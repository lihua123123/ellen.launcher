// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Windows.Graphics;

namespace Ellen.Launcher.UI.Windowing.Abstraction;

public interface IXamlWindowHasInitSize
{
    SizeInt32 InitSize { get; }
}