// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

/// <summary>
/// “我的角色”搜索 token 模板选择：绝区零职业/属性图标保留原色，原神沿用原模板。
/// </summary>
public sealed partial class AvatarSearchTokenTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DefaultTemplate { get; set; }

    public DataTemplate? ZZZTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return item is SearchToken { Kind: SearchTokenKind.ZZZProfession or SearchTokenKind.ZZZElement }
            ? ZZZTemplate
            : DefaultTemplate;
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
