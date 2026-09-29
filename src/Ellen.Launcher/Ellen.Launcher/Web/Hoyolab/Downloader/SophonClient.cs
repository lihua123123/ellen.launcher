// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Downloader;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class SophonClient : ISophonClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial SophonClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<SophonBuild>> GetBuildAsync(BranchWrapper branch, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.SophonChunkGetBuildByBranch(branch))
            .Get();

        Response<SophonBuild>? resp = await builder
            .SendAsync<Response<SophonBuild>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<SophonPatchBuild>> GetPatchBuildAsync(BranchWrapper branch, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.SophonChunkGetPatchBuild())
            .PostJson(branch);

        Response<SophonPatchBuild>? resp = await builder
            .SendAsync<Response<SophonPatchBuild>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}