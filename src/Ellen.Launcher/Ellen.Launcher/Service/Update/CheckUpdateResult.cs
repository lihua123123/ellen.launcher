// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Upstream;

namespace Ellen.Launcher.Service.Update;

public sealed class CheckUpdateResult
{
    public CheckUpdateResultKind Kind { get; set; }

    public EllenPackageInformation? PackageInformation { get; set; }
}