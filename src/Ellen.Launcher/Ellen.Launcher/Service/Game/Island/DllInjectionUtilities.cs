// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.Service.Game.Island;

public static class DllInjectionUtilities
{
    public static unsafe void InjectUsingWindowsHook(ReadOnlySpan<char> dllPath, ReadOnlySpan<char> functionName, int processId)
    {
        fixed (char* pDllPath = dllPath)
        {
            fixed (char* pFunctionName = functionName)
            {
                Marshal.ThrowExceptionForHR(DllInjectionUtilitiesInjectUsingWindowsHook(pDllPath, pFunctionName, processId));
            }
        }
    }

    public static unsafe void InjectUsingWindowsHook2(ReadOnlySpan<char> dllPath, ReadOnlySpan<char> functionName, int processId)
    {
        fixed (char* pDllPath = dllPath)
        {
            fixed (char* pFunctionName = functionName)
            {
                Marshal.ThrowExceptionForHR(DllInjectionUtilitiesInjectUsingWindowsHook2(pDllPath, pFunctionName, processId));
            }
        }
    }

    public static unsafe void InjectUsingRemoteThread(ReadOnlySpan<char> dllPath, int processId)
    {
        fixed (char* pDllPath = dllPath)
        {
            Marshal.ThrowExceptionForHR(DllInjectionUtilitiesInjectUsingRemoteThread(pDllPath, processId));
        }
    }

    public static unsafe void InjectUsingRemoteThread(ReadOnlySpan<char> dllPath, ReadOnlySpan<char> functionName, int processId)
    {
        fixed (char* pDllPath = dllPath)
        {
            fixed (char* pFunctionName = functionName)
            {
                Marshal.ThrowExceptionForHR(DllInjectionUtilitiesInjectUsingRemoteThreadWithFunction(pDllPath, pFunctionName, processId));
            }
        }
    }

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT DllInjectionUtilitiesInjectUsingWindowsHook(PCWSTR dllPath, PCWSTR functionName, int processId);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT DllInjectionUtilitiesInjectUsingWindowsHook2(PCWSTR dllPath, PCWSTR functionName, int processId);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT DllInjectionUtilitiesInjectUsingRemoteThread(PCWSTR dllPath, int processId);

    [DllImport(EllenNativeMethods.DllName, CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
    private static extern HRESULT DllInjectionUtilitiesInjectUsingRemoteThreadWithFunction(PCWSTR dllPath, PCWSTR functionName, int processId);
}
