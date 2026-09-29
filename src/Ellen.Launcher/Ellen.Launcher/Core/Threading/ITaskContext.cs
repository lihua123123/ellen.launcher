// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using JetBrains.Annotations;

namespace Ellen.Launcher.Core.Threading;

public interface ITaskContext : ITaskContextUnsafe
{
    void BeginInvokeOnMainThread([RequireStaticDelegate] Action action);

    void InvokeOnMainThread([InstantHandle] Action action);

    T InvokeOnMainThread<T>([InstantHandle] Func<T> func);

    ThreadPoolSwitchOperation SwitchToBackgroundAsync();

    DispatcherQueueSwitchOperation SwitchToMainThreadAsync();
}