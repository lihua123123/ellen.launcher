// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Hoyolab.Takumi.Auth;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Binding;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class BindingClient
{
    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IApiEndpointsFactory apiEndpointsFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial BindingClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<ListWrapper<UserGameRole>>> GetUserGameRolesOverseaAwareAsync(User user, CancellationToken token = default)
    {
        if (user.IsOversea)
        {
            return await GetUserGameRolesByCookieAsync(user, token).ConfigureAwait(false);
        }

        Response<ActionTicketWrapper> actionTicketResponse = await serviceProvider
            .GetRequiredService<AuthClient>()
            .GetActionTicketBySTokenAsync("game_role", user, token)
            .ConfigureAwait(false);

        if (ResponseValidator.TryValidate(actionTicketResponse, serviceProvider, out ActionTicketWrapper? wrapper))
        {
            string actionTicket = wrapper.Ticket;
            return await GetUserGameRolesByActionTicketAsync(actionTicket, user, token).ConfigureAwait(false);
        }

        return Response.Response.CloneReturnCodeAndMessage<ListWrapper<UserGameRole>, ActionTicketWrapper>(actionTicketResponse);
    }

    public async ValueTask<Response<ListWrapper<UserGameRole>>> GetUserGameRolesByActionTicketAsync(string actionTicket, User user, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(user.IsOversea).UserGameRolesByActionTicket(actionTicket))
            .SetUserCookieAndFpHeader(user, CookieType.LToken)
            .Get();

        Response<ListWrapper<UserGameRole>>? resp = await builder
            .SendAsync<Response<ListWrapper<UserGameRole>>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<ListWrapper<UserGameRole>>> GetUserGameRolesByCookieAsync(User user, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(user.IsOversea).UserGameRolesByCookie())
            .SetUserCookieAndFpHeader(user, CookieType.LToken)
            .Get();

        Response<ListWrapper<UserGameRole>>? resp = await builder
            .SendAsync<Response<ListWrapper<UserGameRole>>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    /// <summary>
    /// 获取绝区零的游戏角色列表。
    /// 优先使用 cookie 方式（国际服/国服通用），失败时回退到 action_ticket（国服绑定接口的常规鉴权方式）。
    /// </summary>
    public async ValueTask<Response<ListWrapper<ZZZUserGameRole>>> GetZZZUserGameRolesAsync(User user, CancellationToken token = default)
    {
        Response<ListWrapper<ZZZUserGameRole>> cookieResponse = await GetZZZUserGameRolesByCookieAsync(user, token).ConfigureAwait(false);
        if (cookieResponse.ReturnCode is 0 || user.IsOversea || string.IsNullOrEmpty(user.Aid))
        {
            // Aid 为空时无法申请 action ticket（会抛 ArgumentException），直接返回 cookie 结果
            return cookieResponse;
        }

        Response<ActionTicketWrapper> actionTicketResponse = await serviceProvider
            .GetRequiredService<AuthClient>()
            .GetActionTicketBySTokenAsync("game_role", user, token)
            .ConfigureAwait(false);

        if (ResponseValidator.TryValidate(actionTicketResponse, serviceProvider, out ActionTicketWrapper? wrapper))
        {
            return await GetZZZUserGameRolesByActionTicketAsync(wrapper.Ticket, user, token).ConfigureAwait(false);
        }

        return cookieResponse;
    }

    public async ValueTask<Response<ListWrapper<ZZZUserGameRole>>> GetZZZUserGameRolesByCookieAsync(User user, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(user.IsOversea).UserGameRolesByCookieZZZ())
            .SetUserCookieAndFpHeader(user, CookieType.LToken)
            .Get();

        Response<ListWrapper<ZZZUserGameRole>>? resp = await builder
            .SendAsync<Response<ListWrapper<ZZZUserGameRole>>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    private async ValueTask<Response<ListWrapper<ZZZUserGameRole>>> GetZZZUserGameRolesByActionTicketAsync(string actionTicket, User user, CancellationToken token = default)
    {
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpointsFactory.Create(user.IsOversea).UserGameRolesByActionTicketZZZ(actionTicket))
            .SetUserCookieAndFpHeader(user, CookieType.LToken)
            .Get();

        Response<ListWrapper<ZZZUserGameRole>>? resp = await builder
            .SendAsync<Response<ListWrapper<ZZZUserGameRole>>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }
}