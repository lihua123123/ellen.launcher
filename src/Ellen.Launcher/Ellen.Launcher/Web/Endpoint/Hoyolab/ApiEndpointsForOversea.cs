// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Endpoint.Hoyolab;

[Service(ServiceLifetime.Singleton, typeof(IApiEndpoints), Key = ApiEndpointsKind.Oversea)]
public sealed class ApiEndpointsForOversea : ApiEndpointsImplementationForOversea;