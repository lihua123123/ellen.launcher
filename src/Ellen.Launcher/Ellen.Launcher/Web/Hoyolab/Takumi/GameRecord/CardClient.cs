// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Geetest;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.DataSigning;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Verification;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;

[HttpClient(HttpClientConfiguration.XRpc)]
public sealed partial class CardClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial CardClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<GeetestVerification>> CreateVerificationAsync(User user, CardVerificationHeaders headers, CancellationToken token)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CardCreateVerification(true))
            .SetUserCookieAndFpHeader(user, CookieType.Cookie)
            .SetHeader("x-rpc-challenge_game", $"{headers.ChallengeGame}")
            .SetHeader("x-rpc-challenge_path", headers.ChallengePath)
            .Get();

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, SaltType.X4, false).ConfigureAwait(false);

        Response<GeetestVerification>? resp = await builder
            .SendAsync<Response<GeetestVerification>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<VerificationResult>> VerifyVerificationAsync(User user, CardVerificationHeaders headers, string challenge, string validate, CancellationToken token)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CardVerifyVerification())
            .SetUserCookieAndFpHeader(user, CookieType.Cookie)
            .SetHeader("x-rpc-challenge_game", $"{headers.ChallengeGame}")
            .SetHeader("x-rpc-challenge_path", headers.ChallengePath)
            .PostJson(new GeetestWebResponse(challenge, validate));

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, SaltType.X4, false).ConfigureAwait(false);

        Response<VerificationResult>? resp = await builder
            .SendAsync<Response<VerificationResult>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<DailyNote.WidgetDailyNote>> GetWidgetDataAsync(User user, CancellationToken token)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CardWidgetData2())
            .SetUserCookieAndFpHeader(user, CookieType.SToken)
            .Get();

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, SaltType.X6, false).ConfigureAwait(false);

        Response<DailyNote.WidgetDailyNote>? resp = await builder
            .SendAsync<Response<DailyNote.WidgetDailyNote>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}