// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using System.Net.Http;
using WebResponse = Ellen.Launcher.Web.Response.Response;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Cdkey;

/// <summary>
/// 国服游戏兑换码兑换。参考 TeyvatGuide 的 webExchangeCdkey 实现：
/// 不带 DS 签名，只带完整账号凭据和 webstatic Referer。
/// </summary>
[HttpClient(HttpClientConfiguration.Default)]
[PrimaryHttpMessageHandler(UseCookies = false)]
public sealed partial class RedeemClient
{
    private const string Referer = "https://webstatic.mihoyo.com/";

    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial RedeemClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<WebResponse> ExchangeAsync(Model.Entity.User user, SignInGameRole role, string code, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(role);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        if (user.IsOversea)
        {
            throw new InvalidOperationException(SH.ViewRedeemOverseaUnsupported);
        }

        if (!IsSupportedRole(role))
        {
            throw new InvalidOperationException(SH.ViewRedeemUnsupportedRegion);
        }

        string encodedCode = Uri.EscapeDataString(code);
        string encodedGameBiz = Uri.EscapeDataString(role.GameBiz);
        string encodedRegion = Uri.EscapeDataString(role.Region);
        string encodedUid = Uri.EscapeDataString(role.Uid);
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        string uri = role.Game switch
        {
            SignInGame.Genshin => apiEndpoints.RedeemCdkeyGenshin(encodedCode, encodedGameBiz, encodedRegion, encodedUid, timestamp),
            SignInGame.ZenlessZoneZero => apiEndpoints.RedeemCdkeyZenlessZoneZero(encodedCode, encodedGameBiz, encodedRegion, encodedUid, timestamp),
            _ => throw new InvalidOperationException(SH.ViewRedeemUnsupportedRegion),
        };

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(uri)
            .SetHeader("Referer", Referer)
            .SetUserCookieAndFpHeader(user, CookieType.All)
            .Get();

        WebResponse? resp = await builder
            .SendAsync<WebResponse>(httpClient, token)
            .ConfigureAwait(false);

        return WebResponse.DefaultIfNull(resp);
    }

    private static bool IsSupportedRole(SignInGameRole role)
    {
        return role.GameBiz switch
        {
            "hk4e_cn" => role.Game is SignInGame.Genshin && role.Region is "cn_gf01" or "cn_qd01",
            "nap_cn" => role.Game is SignInGame.ZenlessZoneZero && role.Region is "prod_gf_cn" or "prod_qd_cn",
            _ => false,
        };
    }
}
