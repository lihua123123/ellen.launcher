// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Ellen.Launcher.ViewModel.Backpack;
using WinRT;

namespace Ellen.Launcher.UI.Xaml.Control;

[DependencyProperty<BackpackItemView>("Item", PropertyChangedCallbackName = nameof(OnItemChanged))]
public sealed partial class ItemFlyoutContent : Microsoft.UI.Xaml.Controls.Control
{
    public ItemFlyoutContent()
    {
        DefaultStyleKey = typeof(ItemFlyoutContent);
    }

    private static void OnItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        d.As<ItemFlyoutContent>().DataContext = e.NewValue as BackpackItemView;
    }
}
