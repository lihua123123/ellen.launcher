// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ellen.Launcher.Model.Formulas;
using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Formulas;
using Windows.Foundation;

namespace Ellen.Launcher.UI.Xaml.View.Page;

/// <summary>
/// 「计算公式」页面（目前只收录原神）。
/// <para>
/// 布局对齐 ellen-wiki 的 <c>formulas</c> 页：左侧章节卡片列表，右侧粘性目录；
/// 目录点击跳转到对应章节，滚动时高亮当前章节。
/// </para>
/// </summary>
public sealed partial class FormulasPage : ScopedPage
{
    public FormulasPage()
    {
        InitializeComponent();
        WireBlockTemplates();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<FormulasViewModel>();
    }

    /// <summary>
    /// 引用块模板要引用块选择器自身（递归），写在 XAML 里会形成解析顺序上的循环，
    /// 因此选择器属性在这里装配。
    /// </summary>
    private void WireBlockTemplates()
    {
        if (Resources["FormulaBlockTemplateSelector"] is not FormulaBlockTemplateSelector selector)
        {
            return;
        }

        selector.Paragraph = GetTemplate("FormulaBlockParagraphTemplate");
        selector.Heading = GetTemplate("FormulaBlockHeadingTemplate");
        selector.Formula = GetTemplate("FormulaBlockFormulaTemplate");
        selector.Table = GetTemplate("FormulaBlockTableTemplate");
        selector.Quote = GetTemplate("FormulaBlockQuoteTemplate");

        DataTemplate? GetTemplate(string key)
        {
            return Resources.TryGetValue(key, out object? value) ? value as DataTemplate : null;
        }
    }

    private void OnTocItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FormulaSection section })
        {
            return;
        }

        if (DataContext is not FormulasViewModel viewModel)
        {
            return;
        }

        int index = viewModel.Sections.IndexOf(section);
        if (index < 0)
        {
            return;
        }

        if (SectionItems.ContainerFromIndex(index) is FrameworkElement container)
        {
            container.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true });
        }
    }

    private void OnSectionScrollerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (DataContext is not FormulasViewModel { Sections.IsDefaultOrEmpty: false } viewModel)
        {
            return;
        }

        int active = 0;
        for (int i = 0; i < viewModel.Sections.Length; i++)
        {
            if (SectionItems.ContainerFromIndex(i) is not FrameworkElement container)
            {
                continue;
            }

            try
            {
                Point position = container.TransformToVisual(SectionScroller).TransformPoint(new Point(0, 0));
                if (position.Y <= 16)
                {
                    active = i;
                }
            }
            catch (ArgumentException)
            {
                // 元素尚未进入可视树，跳过即可。
            }
        }

        SetActiveTocItem(active);
    }

    private void SetActiveTocItem(int index)
    {
        if (TocItems.ItemsPanelRoot is null)
        {
            return;
        }

        Brush? activeBackground = Application.Current is { Resources: { } resources } && resources.TryGetValue("SubtleFillColorSecondaryBrush", out object? value)
            ? value as Brush
            : null;

        for (int i = 0; i < TocItems.Items.Count; i++)
        {
            if (TocItems.ContainerFromIndex(i) is not DependencyObject container)
            {
                continue;
            }

            if (FindDescendant<HyperlinkButton>(container) is not { } button)
            {
                continue;
            }

            button.Background = i == index ? activeBackground : null;
        }
    }

    private static T? FindDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                return typed;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
