// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.Web.WebView2;

namespace Ellen.Launcher.UI.Xaml.View.Page;

/// <summary>
/// 「语言翻译」页面：内嵌空荧酒馆的文本对照工具（<c>https://hoyofan.yuanshen.site/</c>）。
/// 与提瓦特地图一样是整页铺满的网页，XAML 里不套卡片、不留内边距。
/// </summary>
public sealed partial class HoyoFanTranslatePage : ScopedPage
{
    private const string TranslateUri = "https://hoyofan.yuanshen.site/";

    public HoyoFanTranslatePage()
    {
        InitializeComponent();
        TranslateWebView.Loaded += OnWebViewLoaded;
        TranslateWebView.Unloaded += OnWebViewUnloaded;
        TranslateWebView.NavigationCompleted += OnNavigationCompleted;
    }

    private void OnWebViewLoaded(object sender, RoutedEventArgs e)
    {
        WebView2PageLoader.LoadAsync(TranslateWebView, TranslateLoadFailedHint, TranslateUri).SafeForget();
    }

    private void OnNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        WebView2PageLoader.OnNavigationCompleted(TranslateLoadFailedHint, args);
    }

    private void OnWebViewUnloaded(object sender, RoutedEventArgs e)
    {
        TranslateWebView.Loaded -= OnWebViewLoaded;
        TranslateWebView.Unloaded -= OnWebViewUnloaded;
        TranslateWebView.NavigationCompleted -= OnNavigationCompleted;

        TranslateWebView.Close();
    }
}
