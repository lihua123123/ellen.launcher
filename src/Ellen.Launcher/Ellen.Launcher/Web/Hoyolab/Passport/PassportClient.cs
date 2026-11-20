// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.DataSigning;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Ellen.Launcher.Web.Hoyolab.Passport;

[HttpClient(HttpClientConfiguration.XRpc2)]
public sealed partial class PassportClient : IPassportClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Chinese)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial PassportClient(IServiceProvider serviceProvider, HttpClient httpClient);

    // Passport V2 exchange protocol, ported from TeyvatGuide (BTMuli/TeyvatGuide,
    // src/request/passportReq.ts, Beta v0.13.2). The legacy endpoints
    // /account/auth/api/getLTokenBySToken and getCookieAccountInfoBySToken now return
    // retcode 1002 ("系统异常") since late 2026; the mihoyo BBS app itself uses the
    // ma-cn-session/app/exchange endpoint below to trade an SToken for other tokens.
    private const string PassportAppVersion = "2.119.0";
    private const string PassportSdkVersion = "2.44.0";
    private const string PassportUserAgent = "Mozilla/5.0 (Linux; Android 12) Mobile miHoYoBBS/2.119.0";
    private static readonly string PassportLifecycleId = Guid.NewGuid().ToString();

    public async ValueTask<Response<UidCookieToken>> GetCookieAccountInfoBySTokenAsync(User user, CancellationToken token = default)
    {
        Response<WebTokenExchangeData> response = await ExchangeWebTokenAsync(user, PassportTokenType.CookieToken, token).ConfigureAwait(false);
        if (response.ReturnCode is not 0)
        {
            return Response.Response.CloneReturnCodeAndMessage<UidCookieToken, WebTokenExchangeData>(response);
        }

        WebTokenExchangeData? data = response.Data;
        string? uid = !string.IsNullOrEmpty(data?.UserInfo?.Aid) ? data.UserInfo.Aid : user.Aid;
        return new(response.ReturnCode, response.Message, new UidCookieToken
        {
            Uid = uid ?? string.Empty,
            CookieToken = data?.Token?.Token ?? string.Empty,
        });
    }

    public async ValueTask<Response<LTokenWrapper>> GetLTokenBySTokenAsync(User user, CancellationToken token = default)
    {
        Response<WebTokenExchangeData> response = await ExchangeWebTokenAsync(user, PassportTokenType.LToken, token).ConfigureAwait(false);
        if (response.ReturnCode is not 0)
        {
            return Response.Response.CloneReturnCodeAndMessage<LTokenWrapper, WebTokenExchangeData>(response);
        }

        return new(response.ReturnCode, response.Message, new LTokenWrapper
        {
            LToken = response.Data?.Token?.Token ?? string.Empty,
        });
    }

    private async ValueTask<Response<WebTokenExchangeData>> ExchangeWebTokenAsync(User user, PassportTokenType destination, CancellationToken token)
    {
        string? stoken = user.SToken?.GetValueOrDefault(Cookie.STOKEN);
        ArgumentException.ThrowIfNullOrEmpty(stoken);
        ArgumentException.ThrowIfNullOrEmpty(user.Mid);

        // Build the exact JSON body string ourselves. TeyvatGuide escapes < > & ' = \u2028 \u2029
        // after JSON.stringify, and the DS signature is computed over that exact string.
        string body = BuildExchangeBody(destination, stoken, user.Mid);

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.AccountExchangeWebToken())
            .SetHeader("User-Agent", PassportUserAgent)
            .SetHeader("Accept", "application/json")
            .SetHeader("x-rpc-app_version", PassportAppVersion)
            .SetHeader("x-rpc-client_type", "2")
            .SetHeader("x-rpc-app_id", "bll8iq97cem8")
            .SetHeader("x-rpc-sdk_version", PassportSdkVersion)
            .SetHeader("x-rpc-account_version", PassportSdkVersion)
            .SetHeader("x-rpc-lifecycle_id", PassportLifecycleId)
            .SetHeader("x-rpc-game_biz", "bbs_cn")
            .SetHeader("x-rpc-sys_version", "12")
            .SetStringContent(body, Encoding.UTF8, "application/json")
            .Post();

        // SignDataAsync reads the content as string, so the DS "b=" matches body exactly.
        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, SaltType.PROD, true).ConfigureAwait(false);

        Response<WebTokenExchangeData>? resp = await builder
            .SendAsync<Response<WebTokenExchangeData>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    private static string BuildExchangeBody(PassportTokenType destination, string stoken, string mid)
    {
        return string.Concat(
            "{\"dst_token_type\":", ((int)destination).ToString(CultureInfo.InvariantCulture),
            ",\"src_token\":{\"token\":\"", EscapeJson(stoken),
            "\",\"token_type\":", ((int)PassportTokenType.SToken).ToString(CultureInfo.InvariantCulture),
            "},\"mid\":\"", EscapeJson(mid), "\"}");
    }

    private static string EscapeJson(string value)
    {
        StringBuilder builder = new(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '<': builder.Append("\\u003c"); break;
                case '>': builder.Append("\\u003e"); break;
                case '&': builder.Append("\\u0026"); break;
                case '\'': builder.Append("\\u0027"); break;
                case '=': builder.Append("\\u003d"); break;
                case '\u2028': builder.Append("\\u2028"); break;
                case '\u2029': builder.Append("\\u2029"); break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }
        return builder.ToString();
    }

    public async ValueTask<Response<UserInfoWrapper>> VerifyLtokenAsync(User user, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.AccountVerifyLtoken())
            .SetUserCookieAndFpHeader(user, CookieType.LToken)
            .PostJson(new Timestamp());

        Response<UserInfoWrapper>? resp = await builder
            .SendAsync<Response<UserInfoWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<LoginResult>> LoginBySTokenAsync(Cookie stokenV1, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.AccountGetSTokenByOldToken())
            .SetHeader("Cookie", stokenV1.ToString())
            .Post();

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, SaltType.PROD, true).ConfigureAwait(false);

        Response<LoginResult>? resp = await builder
            .SendAsync<Response<LoginResult>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<LoginResult>> LoginByGameTokenAsync(UidGameToken account, CancellationToken token = default)
    {
        AccountIdGameToken data = new()
        {
            AccountId = int.Parse(account.Uid, CultureInfo.InvariantCulture),
            GameToken = account.GameToken,
        };

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.AccountGetSTokenByGameToken())
            .PostJson(data);

        Response<LoginResult>? resp = await builder
            .SendAsync<Response<LoginResult>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<(string? Aigis, Response<MobileCaptcha> Response)> CreateLoginCaptchaAsync(string mobile, string? aigis, CancellationToken token = default)
    {
        Dictionary<string, string> data = new()
        {
            ["area_code"] = Encrypt("+86"),
            ["mobile"] = Encrypt(mobile),
        };

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.AccountCreateLoginCaptcha())
            .PostJson(data);

        if (!string.IsNullOrEmpty(aigis))
        {
            builder.SetXrpcAigis(aigis);
        }

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, SaltType.PROD, true).ConfigureAwait(false);

        (HttpResponseHeaders? headers, Response<MobileCaptcha>? resp) = await builder
            .SendAsync<Response<MobileCaptcha>>(httpClient, token)
            .ConfigureAwait(false);

        string? rpcAigis = headers?.GetValuesOrDefault("X-Rpc-Aigis")?.SingleOrDefault();
        return (rpcAigis, Response.Response.DefaultIfNull(resp));
    }

    public ValueTask<Response<LoginResult>> LoginByMobileCaptchaAsync(IPassportMobileCaptchaProvider provider, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(provider.ActionType);
        ArgumentNullException.ThrowIfNull(provider.Mobile);
        ArgumentNullException.ThrowIfNull(provider.Captcha);

        return LoginByMobileCaptchaAsync(provider.ActionType, provider.Mobile, provider.Captcha, provider.Aigis, token);
    }

    public async ValueTask<Response<LoginResult>> LoginByMobileCaptchaAsync(string actionType, string mobile, string captcha, string? aigis, CancellationToken token = default)
    {
        Dictionary<string, string> data = new()
        {
            ["area_code"] = Encrypt("+86"),
            ["action_type"] = actionType,
            ["captcha"] = captcha,
            ["mobile"] = Encrypt(mobile),
        };

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.AccountLoginByMobileCaptcha())
            .PostJson(data);

        if (!string.IsNullOrEmpty(aigis))
        {
            builder.SetXrpcAigis(aigis);
        }

        await builder.SignDataAsync(DataSignAlgorithmVersion.Gen2, SaltType.PROD, true).ConfigureAwait(false);

        Response<LoginResult>? resp = await builder
            .SendAsync<Response<LoginResult>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public ValueTask<Response<ActionTicketInfo>> GetActionTicketInfoAsync(string ticket, CancellationToken token = default)
    {
        return ValueTask.FromException<Response<ActionTicketInfo>>(new NotSupportedException());
    }

    public ValueTask<Response.Response> VerifyActionTicketPartlyAsync(string ticket, string captcha, CancellationToken token = default)
    {
        return ValueTask.FromException<Response.Response>(new NotSupportedException());
    }

    public ValueTask<(string? Aigis, Response.Response Response)> CreateEmailCaptchaByActionTicketAsync(string ticket, string? aigis, CancellationToken token = default)
    {
        return ValueTask.FromException<(string? Aigis, Response.Response Response)>(new NotSupportedException());
    }

    private static string Encrypt(string source)
    {
        using (RSA rsa = RSA.Create())
        {
            // https://www.rfc-editor.org/rfc/rfc7468#section-3
            // https://github.com/UIGF-org/Hoyolab.Salt#cn-passport-api-rsa_public_key
            rsa.ImportFromPem("""
                -----BEGIN PUBLIC KEY-----
                MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDDvekdPMHN3AYhm/vktJT+YJr7
                cI5DcsNKqdsx5DZX0gDuWFuIjzdwButrIYPNmRJ1G8ybDIF7oDW2eEpm5sMbL9zs
                9ExXCdvqrn51qELbqj0XxtMTIpaCHFSI50PfPpTFV9Xt/hmyVwokoOXFlAEgCn+Q
                CgGs52bFoYMtyi+xEQIDAQAB
                -----END PUBLIC KEY-----
                """);
            return Convert.ToBase64String(rsa.Encrypt(Encoding.UTF8.GetBytes(source), RSAEncryptionPadding.Pkcs1));
        }
    }

    private class Timestamp
    {
        [JsonPropertyName("t")]
        public long Time { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}