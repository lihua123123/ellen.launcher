// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Bridge;

namespace Ellen.Launcher.UI.Xaml.View.Window.WebView2;

[DependencyProperty<string>("ChineseSource")]
[DependencyProperty<string>("OverseaSource")]
public sealed partial class StaticJSBridgeUriSourceProvider : DependencyObject, IJSBridgeUriSourceProvider
{
    public MiHoYoJSBridge CreateJSBridge(IServiceProvider serviceProvider, CoreWebView2 coreWebView2, UserAndUid userAndUid)
    {
        return ActivatorUtilities.CreateInstance<MiHoYoJSBridge>(serviceProvider, coreWebView2, userAndUid);
    }

    public string? GetSource(UserAndUid userAndUid)
    {
        return userAndUid.IsOversea ? OverseaSource : ChineseSource;
    }
}