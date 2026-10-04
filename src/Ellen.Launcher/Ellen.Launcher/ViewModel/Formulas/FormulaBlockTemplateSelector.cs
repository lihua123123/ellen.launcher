// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Model.Formulas;

namespace Ellen.Launcher.ViewModel.Formulas;

/// <summary>
/// 按 <see cref="FormulaBlock.Kind"/> 选择块模板。
/// <para>
/// 模板由页面在 <c>InitializeComponent()</c> 之后从资源里取出来赋给本类：
/// 引用块模板需要引用本选择器自身（递归），写在 XAML 里会形成「选择器属性引用模板、
/// 模板又引用选择器」的循环，改用代码装配就没有解析顺序问题。
/// </para>
/// </summary>
public sealed partial class FormulaBlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Paragraph { get; set; }

    public DataTemplate? Heading { get; set; }

    public DataTemplate? Formula { get; set; }

    public DataTemplate? Table { get; set; }

    public DataTemplate? Quote { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return Select(item);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return Select(item);
    }

    private DataTemplate? Select(object item)
    {
        if (item is not FormulaBlock block)
        {
            return Paragraph;
        }

        return block.Kind switch
        {
            "h" => Heading,
            "formula" => Formula,
            "table" => Table,
            "quote" => Quote,
            _ => Paragraph,
        };
    }
}
