// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Hoyolab;

public static class ApiEndpointsFactoryExtension
{
    extension(IApiEndpointsFactory factory)
    {
        public IApiEndpoints Create(bool isOversea)
        {
            return factory.Create(isOversea ? ApiEndpointsKind.Oversea : ApiEndpointsKind.Chinese);
        }
    }
}