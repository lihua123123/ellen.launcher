// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Bbs.Home;

[HttpClient(HttpClientConfiguration.XRpc2)]
public sealed partial class HomeClient : IHomeClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial HomeClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<NewHomeNewInfo>> GetNewHomeInfoAsync(int gid, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.HomeNew(gid))
            .Get();

        Response<NewHomeNewInfo>? resp = await builder
            .SendAsync<Response<NewHomeNewInfo>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}