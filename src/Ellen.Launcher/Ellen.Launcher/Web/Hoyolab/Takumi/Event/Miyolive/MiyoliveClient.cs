// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Miyolive;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class MiyoliveClient : IMiyoliveClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial MiyoliveClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<CodeListWrapper>> RefreshCodeAsync(string actId, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.MiyoliveRefreshCode())
            .SetHeader("x-rpc-act_id", actId)
            .Get();

        Response<CodeListWrapper>? resp = await builder
            .SendAsync<Response<CodeListWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}