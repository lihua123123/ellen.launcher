// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32.Foundation;

namespace Ellen.Launcher.Core.Diagnostics;

public interface IProcess : IDisposable
{
    int Id { get; }

    nint Handle { get; }

    HWND MainWindowHandle { get; }

    bool HasExited { get; }

    int ExitCode { get; }

    void Start();

    void ResumeMainThread();

    void WaitForExit();

    void Kill();
}