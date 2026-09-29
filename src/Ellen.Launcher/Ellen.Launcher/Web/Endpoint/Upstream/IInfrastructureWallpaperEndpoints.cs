// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

public interface IInfrastructureWallpaperEndpoints : IInfrastructureRootAccess
{
    string WallpaperBing()
    {
        return $"{Root}/wallpaper/bing";
    }

    string WallpaperGenshinLauncher()
    {
        return $"{Root}/wallpaper/hoyoplay";
    }

    string WallpaperToday()
    {
        return $"{Root}/wallpaper/today";
    }
}