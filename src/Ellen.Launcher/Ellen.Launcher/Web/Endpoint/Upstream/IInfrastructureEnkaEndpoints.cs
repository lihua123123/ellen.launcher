// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab;

namespace Ellen.Launcher.Web.Endpoint.Upstream;

public interface IInfrastructureEnkaEndpoints : IInfrastructureRootAccess
{
    string Enka(PlayerUid uid)
    {
        return $"{Root}/enka/{uid}";
    }

    string EnkaPlayerInfo(PlayerUid uid)
    {
        return $"{Root}/enka/{uid}/info";
    }
}