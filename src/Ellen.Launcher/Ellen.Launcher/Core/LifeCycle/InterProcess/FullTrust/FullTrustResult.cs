// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.FullTrust;

public abstract class FullTrustResult
{
    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }
}