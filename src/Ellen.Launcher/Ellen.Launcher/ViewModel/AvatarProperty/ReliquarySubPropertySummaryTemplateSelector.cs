// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public sealed partial class ReliquarySubPropertySummaryTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ReliquaryTemplate { get; set; }

    public DataTemplate? SummaryCardTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            ReliquaryView => ReliquaryTemplate,
            ReliquarySubPropertySummaryCard => SummaryCardTemplate,
            _ => base.SelectTemplateCore(item, container),
        };
    }
}
