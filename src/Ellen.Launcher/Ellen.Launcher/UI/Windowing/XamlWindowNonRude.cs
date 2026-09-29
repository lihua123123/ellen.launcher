// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.UI.Windowing;

public sealed partial class XamlWindowNonRude : IDisposable
{
    private readonly EllenNativeWindowNonRude native;

    public XamlWindowNonRude(HWND hwnd)
    {
        native = EllenNative.Instance.MakeWindowNonRude(hwnd);
        native.Attach();
    }

    public void Dispose()
    {
        try
        {
            native.Detach();
        }
        catch (COMException ex)
        {
            if (EllenNative.IsWin32(ex.HResult, WIN32_ERROR.ERROR_MAPPED_ALIGNMENT))
            {
                return;
            }

            throw;
        }
    }
}