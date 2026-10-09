// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.Web.WebView2;

namespace Ellen.Launcher.UI.Xaml.View.Page;

/// <summary>
/// 「提瓦特地图」页面：以内嵌网页的方式挂载空荧酒馆的提瓦特互动地图。
/// <para>
/// 页面来自 <c>kongying-tavern/map_front_v3</c>（Quasar/Vue3 的全屏地图前端），
/// 自建需要先 <c>quasar build</c> 再自备点位数据后端，所以这里直接挂它的线上构建
/// <c>https://v3.yuanshen.site/</c>。页面按 100vw/100vh 设计，XAML 里不套卡片、不留内边距。
/// 后续若接入原生地图，替换这里的 WebView2 即可。
/// </para>
/// </summary>
public sealed partial class TeyvatMapPage : ScopedPage
{
    private const string MapUri = "https://v3.yuanshen.site/";

    // 来源弹窗每次启动软件只弹一次
    private static bool sourceNoticeShown;

    public TeyvatMapPage()
    {
        InitializeComponent();
        MapWebView.Loaded += OnMapWebViewLoaded;
        MapWebView.Unloaded += OnMapWebViewUnloaded;
        MapWebView.NavigationCompleted += OnMapNavigationCompleted;
    }

    private void OnMapWebViewLoaded(object sender, RoutedEventArgs e)
    {
        WebView2PageLoader.LoadAsync(MapWebView, MapLoadFailedHint, MapUri).SafeForget();

        if (!sourceNoticeShown)
        {
            sourceNoticeShown = true;
            ShowSourceNoticeAsync().SafeForget();
        }
    }

    private void OnMapNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        WebView2PageLoader.OnNavigationCompleted(MapLoadFailedHint, args);
    }

    /// <summary>
    /// 打开页面时弹窗注明地图来源与文档地址。
    /// </summary>
    private async Task ShowSourceNoticeAsync()
    {
        IContentDialogFactory contentDialogFactory = Ioc.Default.GetRequiredService<IContentDialogFactory>();
        TeyvatMapSourceNoticeDialog dialog = await contentDialogFactory
            .CreateInstanceAsync<TeyvatMapSourceNoticeDialog>(Ioc.Default)
            .ConfigureAwait(false);

        await dialog.ShowNoticeAsync().ConfigureAwait(false);
    }

    private void OnMapWebViewUnloaded(object sender, RoutedEventArgs e)
    {
        MapWebView.Loaded -= OnMapWebViewLoaded;
        MapWebView.Unloaded -= OnMapWebViewUnloaded;
        MapWebView.NavigationCompleted -= OnMapNavigationCompleted;

        MapWebView.Close();
    }
}
