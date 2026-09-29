// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Web.WebView2.Core;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Bridge;

namespace Ellen.Launcher.UI.Xaml.View.Window.WebView2;

public interface IJSBridgeUriSourceProvider
{
    MiHoYoJSBridge CreateJSBridge(IServiceProvider serviceProvider, CoreWebView2 coreWebView2, UserAndUid userAndUid);

    string? GetSource(UserAndUid userAndUid);
}