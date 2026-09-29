// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;

namespace Ellen.Launcher.Core.LifeCycle;

public interface ICurrentXamlWindowReference
{
    Window? Window { get; set; }
}