// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Nap.Event.GachaRecord;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class ZZZGachaInfoClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IApiEndpointsFactory apiEndpointsFactory;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial ZZZGachaInfoClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<ZZZGachaLogPage>> GetGachaLogPageAsync(GachaLogTypedQueryOptions options, CancellationToken token = default)
    {
        string query = options.ToQueryString();

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(options.IsOversea).GachaRecordZZZGetGachaLog(query))
            .Get();

        Response<ZZZGachaLogPage>? resp = await builder
            .SendAsync<Response<ZZZGachaLogPage>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}
