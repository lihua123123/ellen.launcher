// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class GachaInfoClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IApiEndpointsFactory apiEndpointsFactory;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial GachaInfoClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<GachaLogPage>> GetGachaLogPageAsync(GachaLogTypedQueryOptions options, CancellationToken token = default)
    {
        string query = options.ToQueryString();

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(options.IsOversea).GachaInfoGetGachaLog(query))
            .Get();

        Response<GachaLogPage>? resp = await builder
            .SendAsync<Response<GachaLogPage>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<BeyondGachaLogPage>> GetBeyondGachaLogPageAsync(GachaLogTypedQueryOptions options, CancellationToken token = default)
    {
        string query = options.ToQueryString();

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(options.IsOversea).GachaInfoGetBeyondGachaLog(query))
            .Get();

        Response<BeyondGachaLogPage>? resp = await builder
            .SendAsync<Response<BeyondGachaLogPage>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}