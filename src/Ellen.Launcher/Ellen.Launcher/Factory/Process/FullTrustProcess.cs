// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.LifeCycle.InterProcess.FullTrust;
using Ellen.Launcher.Win32.Foundation;

namespace Ellen.Launcher.Factory.Process;

public sealed partial class FullTrustProcess : IProcess
{
    private readonly FullTrustNamedPipeClient client = new();
    private global::System.Diagnostics.Process? process;

    public FullTrustProcess(FullTrustProcessStartInfoRequest startInfo)
    {
        client.Create(startInfo);
    }

    public int Id { get => process?.Id ?? throw EllenException.InvalidOperation("Process not created"); }

    public nint Handle { get => process?.Handle ?? throw EllenException.InvalidOperation("Process not created"); }

    public HWND MainWindowHandle { get => process?.MainWindowHandle ?? throw EllenException.InvalidOperation("Process not created"); }

    public bool HasExited { get => process?.HasExited ?? throw EllenException.InvalidOperation("Process not created"); }

    public int ExitCode { get => process?.ExitCode ?? throw EllenException.InvalidOperation("Process not created"); }

    public void Dispose()
    {
        client.Dispose();
        process?.Dispose();
    }

    public void Kill()
    {
        if (process is null)
        {
            throw EllenException.InvalidOperation("Process not created");
        }

        process.Kill();
    }

    public void ResumeMainThread()
    {
        client.ResumeMainThread();
    }

    public void Start()
    {
        uint processId = client.StartProcess();
        process = global::System.Diagnostics.Process.GetProcessById((int)processId);
    }

    public void WaitForExit()
    {
        if (process is null)
        {
            throw EllenException.InvalidOperation("Process not created");
        }

        process.WaitForExit();
    }

    public void LoadLibrary(FullTrustLoadLibraryRequest request)
    {
        client.LoadLibrary(request);
    }
}