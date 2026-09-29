// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.BetterGenshinImpact;

public sealed class PipeRequest<T>
{
    public required PipeRequestKind Kind { get; set; }

    public required T Data { get; set; }
}