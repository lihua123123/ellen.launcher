// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Core.IO.Http;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Bbs.User;

[HttpClient(HttpClientConfiguration.Default)]
[PrimaryHttpMessageHandler(UseCookies = false)]
public sealed partial class UserClientOversea : IUserClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    [FromKeyed(ApiEndpointsKind.Oversea)]
    private readonly IApiEndpoints apiEndpoints;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial UserClientOversea(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<UserFullInfoWrapper>> GetUserFullInfoAsync(Model.Entity.User user, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(user.Aid);

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.UserFullInfoQuery(user.Aid))
            .SetUserCookieAndFpHeader(user, CookieType.LToken)
            .SetOptions(RetryHttpHandler.DisableRetry, true)
            .Get();

        Response<UserFullInfoWrapper>? resp = await builder
            .SendAsync<Response<UserFullInfoWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}