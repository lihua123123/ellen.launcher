// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.DataSigning;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;

/// <summary>
/// 绝区零战绩客户端（米游社/GameRecord）。
/// </summary>
[Service(ServiceLifetime.Transient, typeof(IZZZGameRecordClient))]
[HttpClient(HttpClientConfiguration.XRpc)]
[PrimaryHttpMessageHandler(UseCookies = false)]
public sealed partial class ZZZGameRecordClient : IZZZGameRecordClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IApiEndpointsFactory apiEndpointsFactory;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial ZZZGameRecordClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<ZZZAvatarListWrapper<ZZZAvatarBasic>>> GetZZZAgentsAsync(
        User user,
        string uid,
        string server,
        CancellationToken token = default)
    {
        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(user.IsOversea);
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.GameRecordZZZAvatarBasic(uid, server))
            .SetUserCookieAndFpHeader(user, CookieType.Cookie)
            .SetReferer(apiEndpoints.WebStaticReferer())
            .Get();

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, user.IsOversea ? SaltType.OSX4 : SaltType.X4, false).ConfigureAwait(false);

        Response<ZZZAvatarListWrapper<ZZZAvatarBasic>>? response = await builder
            .SendAsync<Response<ZZZAvatarListWrapper<ZZZAvatarBasic>>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(response);
    }

    public async ValueTask<Response<JsonElement>> GetZZZAgentInfoAsync(
        User user,
        string uid,
        string server,
        int agentId,
        CancellationToken token = default)
    {
        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(user.IsOversea);
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.GameRecordZZZAvatarInfo(uid, server, agentId))
            .SetUserCookieAndFpHeader(user, CookieType.Cookie)
            .SetReferer(apiEndpoints.WebStaticReferer())
            .Get();

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, user.IsOversea ? SaltType.OSX4 : SaltType.X4, false).ConfigureAwait(false);

        Response<JsonElement>? response = await builder
            .SendAsync<Response<JsonElement>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(response);
    }
}
