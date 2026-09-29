// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Property;

namespace Ellen.Launcher.ViewModel.Abstraction;

public interface IViewUnloadAware
{
    IProperty<bool> IsViewUnloaded { get; }
}