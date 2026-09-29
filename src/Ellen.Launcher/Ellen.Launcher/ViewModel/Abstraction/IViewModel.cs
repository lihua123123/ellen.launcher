// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Abstraction;
using Ellen.Launcher.UI.Xaml;

namespace Ellen.Launcher.ViewModel.Abstraction;

public interface IViewModel : IPageScoped, IResurrectable, IViewUnloadAware
{
    CancellationToken CancellationToken { get; set; }

    SemaphoreSlim CriticalSection { get; }

    IDeferContentLoader? DeferContentLoader { get; set; }

    void Uninitialize();
}