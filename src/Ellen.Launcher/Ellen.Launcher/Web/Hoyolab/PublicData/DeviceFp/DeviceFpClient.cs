// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.PublicData.DeviceFp;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class DeviceFpClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial DeviceFpClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<DeviceFpWrapper>> GetFingerprintAsync(DeviceFpData data, CancellationToken token)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.DeviceFpGetFp())
            .PostJson(data);

        Response<DeviceFpWrapper>? resp = await builder
            .SendAsync<Response<DeviceFpWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}