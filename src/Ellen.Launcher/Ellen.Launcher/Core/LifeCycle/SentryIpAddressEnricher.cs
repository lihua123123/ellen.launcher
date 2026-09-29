// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Endpoint.Upstream;
using System.Net.Http;

namespace Ellen.Launcher.Core.LifeCycle;

[Service(ServiceLifetime.Transient)]
public sealed partial class SentryIpAddressEnricher
{
    private readonly IEllenEndpointsFactory ellenEndpointsFactory;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial SentryIpAddressEnricher(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask ConfigureAsync()
    {
        try
        {
            string ip = await httpClient.GetStringAsync(ellenEndpointsFactory.Create().IpString()).ConfigureAwait(false);
            SentrySdk.ConfigureScope(static (scope, ip) => { scope.User.IpAddress = ip; }, ip.Trim('"'));
        }
        catch
        {
            // Man, what can I say?
        }
    }
}