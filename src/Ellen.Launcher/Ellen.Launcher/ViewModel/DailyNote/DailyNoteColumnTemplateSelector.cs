// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Model.Entity;

namespace Ellen.Launcher.ViewModel.DailyNote;

/// <summary>
/// 便笺卡里的一列可能是原神角色，也可能是绝区零角色，按类型挑模板。
/// </summary>
public sealed partial class DailyNoteColumnTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GenshinTemplate { get; set; }

    public DataTemplate? ZenlessTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            ZenlessNoteEntry => ZenlessTemplate,
            DailyNoteEntry => GenshinTemplate,
            _ => GenshinTemplate,
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return SelectTemplateCore(item, null!);
    }
}
