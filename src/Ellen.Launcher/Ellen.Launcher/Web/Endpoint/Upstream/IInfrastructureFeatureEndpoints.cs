// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

public interface IInfrastructureFeatureEndpoints : IInfrastructureRootAccess
{
    string Feature(string name)
    {
        return $"{Root}/client/{name}.json";
    }
}