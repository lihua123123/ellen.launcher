// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32.UI.WindowsAndMessaging;

namespace Ellen.Launcher.UI.Input.LowLevel;

public sealed class LowLevelKeyEventArgs
{
    public LowLevelKeyEventArgs(KBDLLHOOKSTRUCT data)
    {
        Data = data;
    }

    public bool Handled { get; set; }

    public KBDLLHOOKSTRUCT Data { get; }
}