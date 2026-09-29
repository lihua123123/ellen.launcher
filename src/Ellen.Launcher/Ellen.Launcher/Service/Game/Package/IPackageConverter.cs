// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.Package;

public interface IPackageConverter
{
    ValueTask<bool> EnsureGameResourceAsync(PackageConverterContext context);

    ValueTask EnsureDeprecatedFilesAndSDKAsync(PackageConverterDeprecationContext context);
}