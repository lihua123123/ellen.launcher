// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Upstream;

[Service(ServiceLifetime.Singleton, typeof(IEllenEndpoints), Key = EllenEndpointsKind.Release)]
public sealed class EllenEndpointsForRelease : IEllenEndpoints
{
    string IInfrastructureRootAccess.Root { get => ServerDomain.GetApiRoot(); }

    string IInfrastructureRawRootAccess.RawRoot { get => ServerDomain.GetApiRoot(); }
}