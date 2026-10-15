// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using System.Collections.Specialized;
using System.Web;

namespace Ellen.Launcher.Service.GachaLog.QueryProvider;

[Service(ServiceLifetime.Transient, typeof(IGachaLogQueryProvider), Key = RefreshOptionKind.ManualInput)]
public sealed partial class GachaLogQueryManualInputProvider : IGachaLogQueryProvider
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly CultureOptions cultureOptions;

    [GeneratedConstructor]
    public partial GachaLogQueryManualInputProvider(IServiceProvider serviceProvider);

    /// <inheritdoc/>
    public async ValueTask<ValueResult<bool, GachaLogQuery>> GetQueryAsync(GachaGameType game)
    {
        GachaLogUrlDialog dialog = await contentDialogFactory.CreateInstanceAsync<GachaLogUrlDialog>(serviceProvider).ConfigureAwait(false);
        if (await dialog.GetInputUrlAsync().ConfigureAwait(false) is not (true, { } url))
        {
            return new(false, default);
        }

        if ((AfterLast(url, "index.html") ?? AfterLast(url, "getGachaLog")) is not { } queryString)
        {
            return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderManualInputInvalid));
        }

        NameValueCollection query = HttpUtility.ParseQueryString(queryString);
        if (!query.TryGetSingleValue("auth_appid", out string? appId) || appId is not "webview_gacha")
        {
            return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderManualInputInvalid));
        }

        if (GetUrlGame(url) is { } urlGame && urlGame != game)
        {
            return new(false, GachaLogQuery.Invalid(SH.ServiceGachaLogUrlProviderManualInputGameMismatch));
        }

        if (!query.TryGetSingleValue("lang", out string? queryLanguageCode) || !LocaleNames.LanguageCodeFitsCurrentLocale(queryLanguageCode, cultureOptions.LocaleName))
        {
            string message = SH.FormatServiceGachaLogUrlProviderUrlLanguageNotMatchCurrentLocale(queryLanguageCode, cultureOptions.LanguageCode);
            return new(false, GachaLogQuery.Invalid(message));
        }

        return new(true, new(url, game));
    }

    /// <summary>
    /// 从链接中识别所属游戏，无法识别时返回 <see langword="null"/>
    /// </summary>
    private static GachaGameType? GetUrlGame(string url)
    {
        if (url.Contains("/nap/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("public-operation-nap", StringComparison.OrdinalIgnoreCase))
        {
            return GachaGameType.ZZZ;
        }

        if (url.Contains("/hk4e/", StringComparison.OrdinalIgnoreCase)
            || url.Contains("public-operation-hk4e", StringComparison.OrdinalIgnoreCase))
        {
            return GachaGameType.Genshin;
        }

        return null;
    }

    private static string? AfterLast(string url, string match)
    {
        ReadOnlySpan<char> urlSpan = url;

        int index = urlSpan.LastIndexOf(match);
        if (index >= 0)
        {
            index += match.Length;
            return urlSpan[index..].ToString();
        }

        return default;
    }
}