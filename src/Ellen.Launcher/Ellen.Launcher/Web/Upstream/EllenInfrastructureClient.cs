// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Core.IO.Http;
using Ellen.Launcher.Web.Endpoint.Upstream;
using Ellen.Launcher.Web.Upstream.Response;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Win32;
using System.Collections.Immutable;
using System.Net.Http;

namespace Ellen.Launcher.Web.Upstream;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class EllenInfrastructureClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IEllenEndpointsFactory ellenEndpointsFactory;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial EllenInfrastructureClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<EllenResponse<StaticResourceSizeInformation>> GetStaticSizeAsync(CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(StaticResourcesEndpoints.StaticSize())
            .Get();

        EllenResponse<StaticResourceSizeInformation>? resp = await builder.SendAsync<EllenResponse<StaticResourceSizeInformation>>(httpClient, token).ConfigureAwait(false);
        return Web.Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<EllenResponse<IPInformation>> GetIPInformationAsync(CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(ellenEndpointsFactory.Create().Ip())
            .Get();

        EllenResponse<IPInformation>? resp = await builder.SendAsync<EllenResponse<IPInformation>>(httpClient, token).ConfigureAwait(false);
        return Web.Response.Response.DefaultIfNull(resp);
    }

    /// <summary>
    /// Lightweight connectivity probe against the Ellen Launcher API. Used as a fallback when
    /// NCSI's cached connectivity state is unreliable. Returns true only when a valid JSON
    /// response is received, so hijacked/offline connections are reported as unreachable.
    /// </summary>
    public async ValueTask<bool> IsReachableAsync(CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(ellenEndpointsFactory.Create().Ip())
            .SetOptions(RetryHttpHandler.DisableRetry, true)
            .Get();

        EllenResponse<IPInformation>? resp = await builder.SendAsync<EllenResponse<IPInformation>>(httpClient, token).ConfigureAwait(false);
        return resp is not null;
    }

    public async ValueTask<EllenResponse<EllenPackageInformation>> GetEllenVersionInformationAsync(CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(ellenEndpointsFactory.Create().PatchSnapHutao())
            .Get();

        EllenResponse<EllenPackageInformation>? resp = await builder.SendAsync<EllenResponse<EllenPackageInformation>>(httpClient, token).ConfigureAwait(false);
        return Web.Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<EllenResponse> AmIBannedAsync(string uid, CancellationToken token)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(ellenEndpointsFactory.Create().AmIBanned())
            .SetHeader("x-hutao-island-identifier", EllenNative.Instance.ExchangeGameUidForIdentifier1820(uid))
            .Get();

        EllenResponse? resp = await builder.SendAsync<EllenResponse>(httpClient, token).ConfigureAwait(false);
        return Web.Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<EllenResponse<ImmutableArray<GitRepository>>> GetGitRepositoryAsync(string name, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(ellenEndpointsFactory.Create().GitRepository(name))
            .Get();

        EllenResponse<ImmutableArray<GitRepository>>? resp = await builder.SendAsync<EllenResponse<ImmutableArray<GitRepository>>>(httpClient, token).ConfigureAwait(false);
        return Web.Response.Response.DefaultIfNull(resp);
    }
}