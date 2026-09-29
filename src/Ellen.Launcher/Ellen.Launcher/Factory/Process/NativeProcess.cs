// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;

namespace Ellen.Launcher.Factory.Process;

public sealed partial class NativeProcess : IProcess
{
    private readonly EllenNativeProcess process;

    public NativeProcess(EllenNativeProcess process)
    {
        this.process = process;
    }

    public int Id { get => (int)process.Id; }

    public nint Handle { get => process.ProcessHandle; }

    public HWND MainWindowHandle { get => process.MainWindowHandle; }

    public bool HasExited { get => !process.GetExitCodeProcess(out _); }

    public int ExitCode
    {
        get
        {
            process.GetExitCodeProcess(out uint code);
            return (int)code;
        }
    }

    public void Start()
    {
        process.Start();
    }

    public void ResumeMainThread()
    {
        process.ResumeMainThread();
    }

    public void WaitForExit()
    {
        process.WaitForExit();
    }

    public void Kill()
    {
        process.Kill();
    }

    public void Dispose()
    {
    }
}