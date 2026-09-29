// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Update;

public enum CheckUpdateResultKind
{
    None = 0,
    VersionApiInvalidResponse = 1,
    VersionApiInvalidSha256 = 2,
    AlreadyUpdated = 3,

    UpdateAvailable = 4,
}