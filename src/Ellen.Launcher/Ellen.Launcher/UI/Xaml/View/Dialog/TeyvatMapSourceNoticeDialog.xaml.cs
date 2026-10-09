// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

/// <summary>
/// 「提瓦特地图」页面的来源声明：内嵌的是空荧酒馆的线上地图，注明来源与文档地址。
/// </summary>
public sealed partial class TeyvatMapSourceNoticeDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;

    [GeneratedConstructor(InitializeComponent = true)]
    public partial TeyvatMapSourceNoticeDialog(IServiceProvider serviceProvider);

    public async ValueTask ShowNoticeAsync()
    {
        await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false);
    }
}
