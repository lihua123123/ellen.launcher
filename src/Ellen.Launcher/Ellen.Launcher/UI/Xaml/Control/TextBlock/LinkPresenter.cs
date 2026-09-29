// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.ViewModel.Wiki;

namespace Ellen.Launcher.UI.Xaml.Control.TextBlock;

[DependencyProperty<string>("LinkName")]
[DependencyProperty<string>("LinkDescription")]
[DependencyProperty<LinkMetadataContext>("LinkContext")]
public sealed partial class LinkPresenter : ContentControl
{
    public LinkPresenter()
    {
        DefaultStyleKey = typeof(LinkPresenter);
    }
}