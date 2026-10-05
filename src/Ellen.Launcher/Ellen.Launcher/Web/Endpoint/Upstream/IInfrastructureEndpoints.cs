// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

public interface IInfrastructureEndpoints :
    IInfrastructureEnkaEndpoints,
    IInfrastructureStrategyEndpoints,
    IInfrastructureFeatureEndpoints,
    IInfrastructureMetadataEndpoints,
    IInfrastructurePatchEndpoints,
    IInfrastructureGitRepositoryEndpoints,
    IInfrastructureWallpaperEndpoints,
    IInfrastructureRootAccess,
    IInfrastructureRawRootAccess
{
    string Ip()
    {
        return $"{Root}/ip";
    }

    string IpString()
    {
        return $"{Root}/ips";
    }
}