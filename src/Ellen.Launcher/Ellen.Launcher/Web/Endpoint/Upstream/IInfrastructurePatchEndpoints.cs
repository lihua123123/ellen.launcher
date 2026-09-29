// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

public interface IInfrastructurePatchEndpoints : IInfrastructureRootAccess
{
    string PatchYaeAchievement()
    {
        return $"{Root}/patch/yae";
    }

    string PatchSnapHutao()
    {
        return $"{Root}/patch/ellen";
    }
}