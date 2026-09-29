// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Service.Notification;

namespace Ellen.Launcher.UI.Xaml.Control;

public sealed partial class InfoBarTemplateSelector : DataTemplateSelector
{
    public DataTemplate ActionButtonEnabled { get; set; } = default!;

    public DataTemplate ActionButtonDisabled { get; set; } = default!;

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        return item is InfoBarOptions { ActionButtonContent: { }, ActionButtonCommand: { } } ? ActionButtonEnabled : ActionButtonDisabled;
    }
}