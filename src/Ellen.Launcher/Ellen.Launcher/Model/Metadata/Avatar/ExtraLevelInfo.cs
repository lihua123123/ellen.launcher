// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class ExtraLevelInfo
{
    public required ExtraLevelIndexKind Index { get; init; }

    public required uint Level { get; init; }
}