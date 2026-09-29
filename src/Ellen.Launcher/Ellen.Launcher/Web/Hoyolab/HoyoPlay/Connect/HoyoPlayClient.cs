// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Service.Game.Scheme;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.ChannelSDK;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.DeprecatedFile;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Package;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class HoyoPlayClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IApiEndpointsFactory apiEndpointsFactory;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial HoyoPlayClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<GamePackagesWrapper>> GetPackagesAsync(LaunchScheme scheme, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(scheme.IsOversea).HoyoPlayConnectGamePackages(scheme))
            .Get();

        Response<GamePackagesWrapper>? resp = await builder
            .SendAsync<Response<GamePackagesWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<GameChannelSDKsWrapper>> GetChannelSDKAsync(LaunchScheme scheme, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(scheme.IsOversea).HoyoPlayConnectGameChannelSDKs(scheme))
            .Get();

        Response<GameChannelSDKsWrapper>? resp = await builder
            .SendAsync<Response<GameChannelSDKsWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<DeprecatedFileConfigurationsWrapper>> GetDeprecatedFileConfigurationsAsync(LaunchScheme scheme, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(scheme.IsOversea).HoyoPlayConnectDeprecatedFileConfigs(scheme))
            .Get();

        Response<DeprecatedFileConfigurationsWrapper>? resp = await builder
            .SendAsync<Response<DeprecatedFileConfigurationsWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<GameBranchesWrapper>> GetBranchesAsync(LaunchScheme scheme, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(scheme.IsOversea).HoyoPlayConnectGameBranches(scheme))
            .Get();

        Response<GameBranchesWrapper>? resp = await builder
            .SendAsync<Response<GameBranchesWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}