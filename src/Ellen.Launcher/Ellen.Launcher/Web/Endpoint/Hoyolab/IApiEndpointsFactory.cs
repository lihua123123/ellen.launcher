// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Hoyolab;

public interface IApiEndpointsFactory
{
    IApiEndpoints Create(ApiEndpointsKind kind);
}