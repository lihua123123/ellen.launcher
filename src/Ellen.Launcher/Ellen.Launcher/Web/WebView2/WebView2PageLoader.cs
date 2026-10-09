// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Ellen.Launcher.Core;
using WinUIWebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace Ellen.Launcher.Web.WebView2;

/// <summary>
/// 「整页内嵌一个网页」的页面（提瓦特地图 / 语言翻译）共用的加载流程：
/// 建 CoreWebView2 环境 → 导航 → 失败提示。
/// </summary>
internal static class WebView2PageLoader
{
    /// <summary>
    /// 初始化 WebView2 并导航到指定地址；任何失败都只显示提示，不把异常抛给整个应用。
    /// </summary>
    internal static async Task LoadAsync(WinUIWebView2 webView, FrameworkElement loadFailedHint, string uri)
    {
        try
        {
            CoreWebView2EnvironmentOptions options = new()
            {
                AdditionalBrowserArguments = "--do-not-de-elevate",
            };

            string? userDataFolder = RuntimeEnvironment.IsUnpackaged ? EllenRuntime.GetWebView2UserDataDirectory() : null;
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, options);
            await webView.EnsureCoreWebView2Async(environment);
        }
        catch (Exception ex)
        {
            // 缺少 WebView2 运行时（WebView2RuntimeNotFoundException）等情况不要连累整个应用，只提示用户
            ShowLoadFailedHint(loadFailedHint, ex);
            return;
        }

        // CoreWebView2 偶尔在 EnsureCoreWebView2Async 之后仍不可用
        if (!SpinWait.SpinUntil(() => webView.CoreWebView2 is not null, TimeSpan.FromSeconds(1)))
        {
            ShowLoadFailedHint(loadFailedHint, null);
            return;
        }

        webView.CoreWebView2.DisableDevToolsForReleaseBuild();
        webView.CoreWebView2.Navigate(uri);
    }

    /// <summary>
    /// 网页导航结束时切换失败提示的可见性。
    /// </summary>
    internal static void OnNavigationCompleted(FrameworkElement loadFailedHint, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            loadFailedHint.Visibility = Visibility.Collapsed;
            return;
        }

        ShowLoadFailedHint(loadFailedHint, null);
    }

    private static void ShowLoadFailedHint(FrameworkElement loadFailedHint, Exception? exception)
    {
        loadFailedHint.Visibility = Visibility.Visible;

        if (exception is not null)
        {
            SentrySdk.CaptureException(exception);
        }
    }
}
