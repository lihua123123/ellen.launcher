// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Response;
using System.Collections.Specialized;
using System.Web;

namespace Ellen.Launcher.Service.GachaLog.QueryProvider;

[Service(ServiceLifetime.Transient, typeof(IGachaLogQueryProvider), Key = RefreshOptionKind.SToken)]
public sealed partial class GachaLogQuerySTokenProvider : IGachaLogQueryProvider
{
    private readonly BindingClient bindingClient;
    private readonly BindingClient2 bindingClient2;
    private readonly CultureOptions cultureOptions;
    private readonly IUserService userService;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial GachaLogQuerySTokenProvider(IServiceProvider serviceProvider);

    public async ValueTask<ValueResult<bool, GachaLogQuery>> GetQueryAsync(GachaGameType game)
    {
        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            return new(false, GachaLogQuery.Invalid(SH.MustSelectUserAndUid));
        }

        if (userAndUid.IsOversea)
        {
            return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderStokenUnsupported));
        }

        GenAuthKeyData data;
        if (game is GachaGameType.ZZZ)
        {
            Response<ListWrapper<ZZZUserGameRole>> roleResponse = await bindingClient
                .GetZZZUserGameRolesAsync(userAndUid.User)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(roleResponse, messenger, out ListWrapper<ZZZUserGameRole>? roles))
            {
                return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderAuthkeyRequestFailed));
            }

            if (roles is null || roles.List.Length <= 0)
            {
                return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogZZZNoGameRoleFound));
            }

            data = GenAuthKeyData.CreateForZZZWebViewGacha(roles.List[0].GameUid, roles.List[0].Region);
        }
        else
        {
            data = GenAuthKeyData.CreateForWebViewGacha(userAndUid.Uid);
        }

        Response<GameAuthKey> authKeyResponse = await bindingClient2.GenerateAuthenticationKeyAsync(userAndUid.User, data).ConfigureAwait(false);

        if (!ResponseValidator.TryValidate(authKeyResponse, messenger, out GameAuthKey? authKey))
        {
            return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderAuthkeyRequestFailed));
        }

        return new(true, new(ComposeQueryString(data, authKey, cultureOptions.LanguageCode, game), game));
    }

    private static string ComposeQueryString(GenAuthKeyData genAuthKeyData, GameAuthKey gameAuthKey, string lang, GachaGameType game)
    {
        // HttpUtility.ParseQueryString(string.Empty) creates an HttpUtility.HttpQSCollection
        // Whose ToString() method returns a query string without the leading '?' character
        NameValueCollection collection = HttpUtility.ParseQueryString(string.Empty);
        collection.Set("lang", lang);
        collection.Set("auth_appid", genAuthKeyData.AuthAppId);
        collection.Set("authkey", gameAuthKey.AuthKey);
        collection.Set("authkey_ver", $"{gameAuthKey.AuthKeyVersion:D}");
        collection.Set("sign_type", $"{gameAuthKey.SignType:D}");

        if (game is GachaGameType.ZZZ)
        {
            // 绝区零接口要求显式携带区服
            collection.Set("region", genAuthKeyData.Region.Value);
            collection.Set("game_biz", genAuthKeyData.GameBiz);
        }

        string? result = collection.ToString();
        ArgumentNullException.ThrowIfNull(result);
        return result;
    }
}
