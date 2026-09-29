// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Ellen.Launcher.UI.Windowing;
using Ellen.Launcher.Win32.Foundation;

namespace Ellen.Launcher.Core.LifeCycle;

public static class CurrentXamlWindowReferenceExtension
{
    extension(ICurrentXamlWindowReference reference)
    {
        public XamlRoot? XamlRoot { get => reference.Window?.Content?.XamlRoot; }

        public HWND WindowHandle { get => reference.Window?.GetWindowHandle() ?? default; }

        public ElementTheme RequestedTheme
        {
            get
            {
                ArgumentNullException.ThrowIfNull(reference.Window);
                return ((FrameworkElement)reference.Window.Content).RequestedTheme;
            }
        }
    }
}