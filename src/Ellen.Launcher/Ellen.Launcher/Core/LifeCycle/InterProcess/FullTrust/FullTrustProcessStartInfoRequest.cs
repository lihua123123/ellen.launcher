// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Win32.System.Threading;

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.FullTrust;

public sealed class FullTrustProcessStartInfoRequest
{
    public required string ApplicationName { get; set; }

    public required string CommandLine { get; set; }

    public required PROCESS_CREATION_FLAGS CreationFlags { get; set; }

    public required string CurrentDirectory { get; set; }
}