// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.ViewModel.Wiki;

namespace Ellen.Launcher.UI.Xaml.Control.TextBlock;

[DependencyProperty<string>("LinkName")]
[DependencyProperty<string>("LinkDescription", PropertyChangedCallbackName = nameof(OnLinkDescriptionChanged))]
[DependencyProperty<Visibility>("DescriptionVisibility")]
[DependencyProperty<LinkMetadataContext>("LinkContext")]
public sealed partial class LinkPresenter : ContentControl
{
    public LinkPresenter()
    {
        DefaultStyleKey = typeof(LinkPresenter);
    }

    /// <summary>
    /// 有些词条只有名字、没有官方释义（瓦列里的「冲击势能」「重击臼炮直射·抵近轰击」就是：
    /// 游戏 TextMap 里只有正文引用，没有独立释义条目，AOG 也没导出 HyperLink 表）。
    /// 这种情况把描述那一块收起来 —— 留一张空白卡片看起来像界面坏了。
    /// </summary>
    private static void OnLinkDescriptionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        ((LinkPresenter)sender).DescriptionVisibility =
            string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
    }
}
