// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Ellen.Launcher.Core.Setting;

namespace Ellen.Launcher.UI.Xaml.Control.Card;

public sealed class CardReference
{
    /// <summary>
    /// 卡片控件。点击即导航的卡片是 <see cref="Microsoft.UI.Xaml.Controls.Button"/>，
    /// 内含多个可交互元素的卡片（如游戏启动卡片）是 <see cref="Microsoft.UI.Xaml.Controls.UserControl"/>。
    /// </summary>
    public FrameworkElement? Card { get; set; }

    public int Order { get; set; }

    public static CardReference Create(FrameworkElement card, string order)
    {
        return new() { Card = card, Order = LocalSetting.Get(order, 0) };
    }
}
