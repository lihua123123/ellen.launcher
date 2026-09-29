// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;
using Ellen.Launcher.Win32.UI.WindowsAndMessaging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Ellen.Launcher.Win32.ConstValues;

namespace Ellen.Launcher.UI.Input.LowLevel;

public delegate void InputLowLevelKeyboardSourceEventHandler(LowLevelKeyEventArgs args);

public static class InputLowLevelKeyboardSource
{
    public static event InputLowLevelKeyboardSourceEventHandler? KeyDown;

    public static event InputLowLevelKeyboardSourceEventHandler? KeyUp;

    public static event InputLowLevelKeyboardSourceEventHandler? SystemKeyDown;

    public static event InputLowLevelKeyboardSourceEventHandler? SystemKeyUp;

    [field: MaybeNull]
    private static EllenNativeInputLowLevelKeyboardSource Native
    {
        get => LazyInitializer.EnsureInitialized(ref field, EllenNative.Instance.MakeInputLowLevelKeyboardSource);
    }

    public static unsafe void Initialize()
    {
        Native.Attach(EllenNativeInputLowLevelKeyboardSourceCallback.Create(&ProcessLowLevelKeyboard));
    }

    public static unsafe void Uninitialize()
    {
        Native.Detach(EllenNativeInputLowLevelKeyboardSourceCallback.Create(&ProcessLowLevelKeyboard));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL ProcessLowLevelKeyboard(uint param, KBDLLHOOKSTRUCT* data)
    {
        LowLevelKeyEventArgs args = new(*data);
        switch (param)
        {
            case WM_KEYDOWN:
                KeyDown?.Invoke(args);
                break;
            case WM_KEYUP:
                KeyUp?.Invoke(args);
                break;
            case WM_SYSKEYDOWN:
                SystemKeyDown?.Invoke(args);
                break;
            case WM_SYSKEYUP:
                SystemKeyUp?.Invoke(args);
                break;
        }

        return args.Handled;
    }
}