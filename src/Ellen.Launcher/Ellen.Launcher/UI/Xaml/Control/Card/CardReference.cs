// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core.Setting;

namespace Ellen.Launcher.UI.Xaml.Control.Card;

public sealed class CardReference
{
    public Button? Card { get; set; }

    public int Order { get; set; }

    public static CardReference Create(Button card, string order)
    {
        return new() { Card = card, Order = LocalSetting.Get(order, 0) };
    }
}