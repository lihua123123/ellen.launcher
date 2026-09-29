// Copyright (c) Snap Hutao RP. All rights reserved.
// Licensed under the MIT license.

using System.Runtime.InteropServices;

namespace Ellen.Launcher.Service.FileUnlocker;

[SuppressMessage("", "SYSLIB1054")]
public static unsafe class EllenFileUnlockerInterop
{
    private const string DllName = "Snap.Hutao.FileUnlocker.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, EntryPoint = "HutaoFileUnlocker_QueryZoneIdentifier")]
    public static extern int QueryZoneIdentifier(char* path, char** metadataJson);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, EntryPoint = "HutaoFileUnlocker_RemoveZoneIdentifier")]
    public static extern int RemoveZoneIdentifier(char* path, [MarshalAs(UnmanagedType.U1)] bool recursive, char** metadataJson);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, EntryPoint = "HutaoFileUnlocker_QueryFileLocks")]
    public static extern int QueryFileLocks(char* path, char** metadataJson);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, EntryPoint = "HutaoFileUnlocker_UnlockFileLocks")]
    public static extern int UnlockFileLocks(char* path, [MarshalAs(UnmanagedType.U1)] bool force, char** metadataJson);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true, EntryPoint = "HutaoFileUnlocker_FreeString")]
    public static extern void FreeString(char* value);
}
