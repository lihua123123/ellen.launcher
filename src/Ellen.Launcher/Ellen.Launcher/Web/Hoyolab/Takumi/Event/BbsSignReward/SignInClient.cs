// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.Geetest;
using Ellen.Launcher.ViewModel.User;
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

    public async ValueTask<Response<SignInRewardInfo>> GetInfoAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.LunaSolInfo(userAndUid.Uid, cultureOptions.LanguageCode))
            .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", "hk4e")
            .Get();

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen1, SaltType.LK2, true).ConfigureAwait(false);

        Response<SignInRewardInfo>? resp = await builder
            .SendAsync<Response<SignInRewardInfo>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<SignInRewardReSignInfo>> GetResignInfoAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.LunaSolResignInfo(userAndUid.Uid, cultureOptions.LanguageCode))
            .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", "hk4e")
            .Get();

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen1, SaltType.LK2, true).ConfigureAwait(false);

        Response<SignInRewardReSignInfo>? resp = await builder
            .SendAsync<Response<SignInRewardReSignInfo>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<Reward>> GetRewardAsync(Model.Entity.User user, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.LunaSolHome(cultureOptions.LanguageCode))
            .SetUserCookieAndFpHeader(user, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", "hk4e")
            .Get();

        Response<Reward>? resp = await builder
            .SendAsync<Response<Reward>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<SignInResult>> ReSignAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.LunaSolReSign(cultureOptions.LanguageCode))
            .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", "hk4e")
            .PostJson(new SignInData(apiEndpoints, userAndUid.Uid));

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
                    .SetHeader("x-rpc-signgame", "hk4e")
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

    public async ValueTask<Response<SignInResult>> SignAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.LunaSolSign())
            .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
            .SetHeader("x-rpc-signgame", "hk4e")
            .PostJson(new SignInData(apiEndpoints, userAndUid.Uid));

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
                    .SetHeader("x-rpc-signgame", "hk4e")
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
}