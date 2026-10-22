// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Service;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;

[HttpClient(HttpClientConfiguration.Default)]
[PrimaryHttpMessageHandler(UseCookies = false)]
public sealed partial class SignInClientOversea : ISignInClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly CultureOptions cultureOptions;
    [FromKeyed(ApiEndpointsKind.Oversea)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial SignInClientOversea(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<SignInRewardInfo>> GetInfoAsync(Model.Entity.User user, SignInGameRole role, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(GetInfoUri(role))
            .SetUserCookieAndFpHeader(user, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", role.SignGameHeader)
            .Get();

        Response<SignInRewardInfo>? resp = await builder
            .SendAsync<Response<SignInRewardInfo>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<SignInResult>> SignAsync(Model.Entity.User user, SignInGameRole role, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(GetSignUri(role))
            .SetUserCookieAndFpHeader(user, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", role.SignGameHeader)
            .PostJson(new SignInGameData(GetActivityId(role), role));

        Response<SignInResult>? resp = await builder
            .SendAsync<Response<SignInResult>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    private string GetInfoUri(SignInGameRole role)
    {
        return role.Game switch
        {
            SignInGame.StarRail => apiEndpoints.LunaInfoStarRail(cultureOptions.LanguageCode, role),
            SignInGame.ZenlessZoneZero => apiEndpoints.LunaInfoZenlessZoneZero(cultureOptions.LanguageCode, role),
            _ => apiEndpoints.LunaInfoGenshin(cultureOptions.LanguageCode, role),
        };
    }

    private string GetSignUri(SignInGameRole role)
    {
        return role.Game switch
        {
            SignInGame.StarRail => apiEndpoints.LunaSignStarRail(),
            SignInGame.ZenlessZoneZero => apiEndpoints.LunaSignZenlessZoneZero(),
            _ => apiEndpoints.LunaSignGenshin(),
        };
    }

    private string GetActivityId(SignInGameRole role)
    {
        return role.Game switch
        {
            SignInGame.StarRail => apiEndpoints.LunaActivityIdStarRail(),
            SignInGame.ZenlessZoneZero => apiEndpoints.LunaActivityIdZenlessZoneZero(),
            _ => apiEndpoints.LunaActivityIdGenshin(),
        };
    }
}
