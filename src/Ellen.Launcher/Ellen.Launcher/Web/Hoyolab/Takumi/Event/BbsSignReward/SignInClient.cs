// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.Geetest;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.DataSigning;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;

[HttpClient(HttpClientConfiguration.XRpc)]
[PrimaryHttpMessageHandler(UseCookies = false)]
public sealed partial class SignInClient : ISignInClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IGeetestService geetestService;
    private readonly CultureOptions cultureOptions;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial SignInClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<SignInRewardInfo>> GetInfoAsync(Model.Entity.User user, SignInGameRole role, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(GetInfoUri(role))
            .SetUserCookieAndFpHeader(user, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", role.SignGameHeader)
            .Get();

        ApplyActHeaders(builder, role);

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen1, SaltType.LK2, true).ConfigureAwait(false);

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

        ApplyActHeaders(builder, role);

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen1, SaltType.LK2, true).ConfigureAwait(false);

        Response<SignInResult>? resp = await builder
            .SendAsync<Response<SignInResult>>(httpClient, token)
            .ConfigureAwait(false);

        if (resp is { Data: { Success: 1, Gt: { } gt, Challenge: { } originChallenge } })
        {
            if (await geetestService.TryVerifyGtChallengeAsync(gt, originChallenge, false, token).ConfigureAwait(false) is { } data)
            {
                builder
                    .Resurrect()
                    .SetHeader("x-rpc-signgame", role.SignGameHeader)
                    .SetXrpcChallenge(data.Challenge, data.Validate);

                await builder.SignDataAsync(DataSignAlgorithmVersion.Gen1, SaltType.LK2, true).ConfigureAwait(false);

                resp = await builder
                    .SendAsync<Response<SignInResult>>(httpClient, token)
                    .ConfigureAwait(false);
            }
            else
            {
                resp.ReturnCode = resp.Data.RiskCode;
                resp.Message = SH.ServiceSignInRiskVerificationFailed;
            }
        }

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

    private static void ApplyActHeaders(HttpRequestMessageBuilder builder, SignInGameRole role)
    {
        // 绝区零签到活动挂在 act.mihoyo.com 下，缺少这两个头会 403
        if (role.Game is SignInGame.ZenlessZoneZero)
        {
            builder
                .SetHeader("Origin", "https://act.mihoyo.com")
                .SetHeader("Referer", "https://act.mihoyo.com/");
        }
    }
}
