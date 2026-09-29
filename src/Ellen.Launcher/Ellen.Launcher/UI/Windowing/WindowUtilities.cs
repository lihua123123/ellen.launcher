// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.UI.Windowing;

[SuppressMessage("", "SYSLIB1054")]
public static unsafe class WindowUtilities
{
    public static void SwitchToWindow(HWND hWnd)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesSwitchToWindow(hWnd));
    }

    public static void AddExtendedStyleLayered(HWND hWnd)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesAddExtendedStyleLayered(hWnd));
    }

    public static void RemoveExtendedStyleLayered(HWND hWnd)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesRemoveExtendedStyleLayered(hWnd));
    }

    public static void SetLayeredWindowTransparency(HWND hWnd, byte opacity)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesSetLayeredWindowTransparency(hWnd, opacity));
    }

    public static void AddExtendedStyleToolWindow(HWND hWnd)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesAddExtendedStyleToolWindow(hWnd));
    }

    public static void RemoveStyleOverlappedWindow(HWND hWnd)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesRemoveStyleOverlappedWindow(hWnd));
    }

    public static float GetRasterizationScaleForWindow(HWND hWnd)
    {
        float scale;
        Marshal.ThrowExceptionForHR(WindowUtilitiesGetRasterizationScaleForWindow(hWnd, &scale));
        return scale;
    }

    public static void SetWindowIsEnabled(HWND hWnd, BOOL isEnabled)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesSetWindowIsEnabled(hWnd, isEnabled));
    }

    public static void SetWindowOwner(HWND hWnd, HWND hWndOwner)
    {
        Marshal.ThrowExceptionForHR(WindowUtilitiesSetWindowOwner(hWnd, hWndOwner));
    }

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesSwitchToWindow(HWND hWnd);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesAddExtendedStyleLayered(HWND hWnd);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesRemoveExtendedStyleLayered(HWND hWnd);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesSetLayeredWindowTransparency(HWND hWnd, byte opacity);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesAddExtendedStyleToolWindow(HWND hWnd);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesRemoveStyleOverlappedWindow(HWND hWnd);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesGetRasterizationScaleForWindow(HWND hWnd, float* scale);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesSetWindowIsEnabled(HWND hWnd, BOOL isEnabled);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT WindowUtilitiesSetWindowOwner(HWND hWnd, HWND hWndOwner);
}