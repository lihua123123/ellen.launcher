// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.Model;

public sealed class ElevationStatusResponse
{
    public ElevationStatusResponse(bool isElevated, int processId)
    {
        IsElevated = isElevated;
        ProcessId = processId;
    }

    public bool IsElevated { get; }

    public int ProcessId { get; }
}
