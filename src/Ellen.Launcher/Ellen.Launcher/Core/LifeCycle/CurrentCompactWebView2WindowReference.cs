// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Ellen.Launcher.UI.Xaml.View.Window.WebView2;

namespace Ellen.Launcher.Core.LifeCycle;

[Service(ServiceLifetime.Singleton, typeof(ICurrentXamlWindowReference), Key = typeof(CompactWebView2Window))]
public sealed class CurrentCompactWebView2WindowReference : ICurrentXamlWindowReference
{
    private readonly WeakReference<CompactWebView2Window> reference = new(default!);

    public Window? Window
    {
        get
        {
            reference.TryGetTarget(out CompactWebView2Window? window);
            return window!;
        }
        set => reference.SetTarget((value as CompactWebView2Window)!);
    }
}