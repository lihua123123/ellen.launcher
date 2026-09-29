// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Backpack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class BackpackPage : ScopedPage
{
    /// <summary>
    /// 列表面板在视口外缓冲的视口数（ItemsWrapGrid.CacheLength）。实测四档（武器页 391 件）：
    /// <list type="table">
    /// <item><term>0</term><description>realized 148，进入卡顿 ~1.1 s</description></item>
    /// <item><term>1.0</term><description>realized 312，6265 / 6881 ms</description></item>
    /// <item><term>2.0</term><description>realized 391，11969 / 11746 ms</description></item>
    /// <item><term>4.0（默认）</term><description>realized 391，11930 / 12256 ms</description></item>
    /// </list>
    /// 单调且在「全量实例化」处饱和。滚动顺滑度换不来 5~11 s，所以定稿 0。
    /// </summary>
    private const double PanelCacheLength = 0D;

    private readonly GridView[] categoryGrids;
    private ScrollViewer? hostScrollViewer;

    /// <summary>
    /// 「列表 → 宿主 ScrollViewer」路径上的水平 chrome 合计（每层 Margin + Padding + BorderThickness，实测）。
    /// 这里曾经写死 32（只减外层 Border 的 Margin 16×2），结果列表比容器宽约 22 px →
    /// 内部滚动视口错位 →「完全不能滚动」。所以改为逐层实测，不再猜常数。
    /// </summary>
    /// <summary>
    /// GridView 自身模板的左右 Padding（XAML 里 `Padding="16,16,10,10"`），它不占 GridView 的宽度。
    /// </summary>
    private const double GridViewHorizontalPadding = 26D;

    private double horizontalChrome = double.NaN;
    private readonly Dictionary<string, double> naturalWidths = [];

    public BackpackPage()
    {
        InitializeComponent();

        categoryGrids = [WeaponGrid, ReliquaryGrid, UpgradeItemGrid, FoodGrid, MaterialGrid, GadgetGrid, QuestGrid, PreciousItemGrid, UnknownGrid];

        foreach (GridView grid in categoryGrids)
        {
            grid.Loaded += OnCategoryGridLoaded;
        }
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<BackpackViewModel>();
    }

    /// <summary>
    /// 命中 Frame 缓存（`BackpackPage.xaml` 根节点上的 `NavigationCacheMode="Required"`）：
    /// 离开视觉树时不拆 ViewModel，返回时 8 个列表的已实现容器还在，第二次进入就免去那 ~1.1 s。
    /// </summary>
    protected override bool PreserveOnUnload => true;

    /// <summary>
    /// Pivot 把内容放在一个**横向可滚动**的 ScrollViewer 里，列表因此被以**无限宽度**测量。
    /// 拿到无限宽度的 ItemsWrapGrid 永远不会换行，只能把每一项都测一遍、并把整个背包实例化出来，
    /// 这正是进入本页卡顿的原因。把宽度夹到内容视口宽度，列表就会恢复正常的虚拟化。
    /// 只设 Width：再设 ItemWidth 会锁死列数并破坏滚动。
    /// </summary>
    private void OnCategoryGridLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not GridView grid)
        {
            return;
        }

        // 列表自身模板里的 ScrollViewer 是它的后代而不是祖先，所以第一个 ScrollViewer 祖先
        // 就是 Pivot 的内容宿主 —— 我们要的正是它的视口宽度。
        if (hostScrollViewer is null && FindAncestor<ScrollViewer>(grid) is { } host)
        {
            hostScrollViewer = host;
            host.SizeChanged += OnHostSizeChanged;
        }

        ApplyListWidth();

        // ItemWidth 只在“首次有容器实现”那一刻钉一次，之后永不变。
        PinItemWidthWhenFirstFilled(grid);
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyListWidth();
    }

    private void ApplyListWidth()
    {
        if (hostScrollViewer is not { ViewportWidth: > 0D } host)
        {
            return;
        }

        if (double.IsNaN(horizontalChrome))
        {
            horizontalChrome = MeasureHorizontalChrome(categoryGrids[0], host);

            if (double.IsNaN(horizontalChrome))
            {
                return;
            }
        }

        double width = host.ViewportWidth - horizontalChrome;
        if (width <= 0D)
        {
            return;
        }

        foreach (GridView grid in categoryGrids)
        {
            grid.Width = width;

            if (grid.ItemsPanelRoot is ItemsWrapGrid panel)
            {
                panel.Orientation = Orientation.Horizontal;
                panel.CacheLength = PanelCacheLength;
                ApplyItemWidth(grid, panel);
            }
        }
    }

    /// <summary>
    /// 等到该 GridView **第一次有容器实现**时，量出它的自然单元宽并一次性钉死 `ItemWidth`。
    ///
    /// 为什么必须“先钉死、之后永不变”：之前是“发现列数超标才改值”，而列数检测跑在 4 s 探针的节拍上，
    /// 滞后于状态 —— 第一次进入的 +8.0 s 处才检测到 9 列并改值，紧接着就是一次 1.1 s 卡顿：
    /// 那正是“改 ItemWidth → 整个面板带着 169 个已实现容器重排”的代价。
    /// 钉死之后值不再变，重排不会发生，8↔9 列的摆动也一并消失。
    /// </summary>
    private void PinItemWidthWhenFirstFilled(GridView grid)
    {
        void OnFirstLayout(object? sender, object e)
        {
            if (grid.ItemsPanelRoot is not ItemsWrapGrid panel
                || VisualTreeHelper.GetChildrenCount(panel) == 0
                || VisualTreeHelper.GetChild(panel, 0) is not FrameworkElement firstChild
                || firstChild.DesiredSize.Width <= 0D)
            {
                return;
            }

            grid.LayoutUpdated -= OnFirstLayout;

            naturalWidths[grid.Name] = firstChild.DesiredSize.Width;
            ApplyItemWidth(grid, panel);
        }

        grid.LayoutUpdated += OnFirstLayout;
    }

    /// <summary>
    /// 单元格宽按**自然宽算列数、再均分**定，语义基于实测的 floor：
    /// <code>
    /// avail  = 宿主 ViewportWidth − chrome − 26（GridView Padding）
    /// cols   = max(1, (int)(avail / natural))     // 武器 2297/276 → 8；圣遗物 2297/326 → 7
    /// ItemWidth = floor(avail / cols)             // 武器 287；圣遗物 328
    /// </code>
    /// 两条都是必需的：<br/>
    /// - 不少分均分：武器自然宽下 2297/276 = 8.3，但实测会排成 9 列、第 9 列被裁；<br/>
    /// - 均分而不 floor：`avail/cols` 的余数会让浮点恰好跨过整数，掉到上一档。
    /// 圣遗物 328 &gt; 设计宽 326，`Border Width="320"` 的卡不会被压扁。<br/>
    /// 宽度来源用**宿主视口**，不用会跳的 `panel.ActualWidth`。
    /// </summary>
    private void ApplyItemWidth(GridView grid, ItemsWrapGrid panel)
    {
        if (!naturalWidths.TryGetValue(grid.Name, out double natural) || natural <= 0D)
        {
            return;
        }

        if (hostScrollViewer is not { ViewportWidth: > 0D } host)
        {
            return;
        }

        double available = host.ViewportWidth - horizontalChrome - GridViewHorizontalPadding;
        if (available <= 0D)
        {
            return;
        }

        int columns = Math.Max(1, (int)(available / natural));
        panel.ItemWidth = Math.Floor(available / columns);
    }

    private static T? FindAncestor<T>(DependencyObject child)
        where T : DependencyObject
    {
        DependencyObject? current = VisualTreeHelper.GetParent(child);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// 累加「列表 → 宿主 ScrollViewer」路径上每一层的水平占宽（Margin + Padding + BorderThickness），并逐层打印，
    /// 方便直接和 XAML 对账。不能再减列表**自身**的 Padding（那在它宽度内部），所以从 Parent 开始走。
    /// 走不到宿主就返回 NaN —— 宁可不设宽度，也不要设一个错的宽度。
    /// </summary>
    private static double MeasureHorizontalChrome(GridView grid, ScrollViewer host)
    {
        double chrome = 0D;
        DependencyObject? current = VisualTreeHelper.GetParent(grid);

        while (current is not null)
        {
            if (ReferenceEquals(current, host))
            {
                return chrome;
            }

            double margin = 0D;
            double padding = 0D;
            double thickness = 0D;

            if (current is FrameworkElement element)
            {
                margin = element.Margin.Left + element.Margin.Right;
            }

            if (current is Border border)
            {
                padding = border.Padding.Left + border.Padding.Right;
                thickness = border.BorderThickness.Left + border.BorderThickness.Right;
            }
            else if (current is Microsoft.UI.Xaml.Controls.Control control)
            {
                padding = control.Padding.Left + control.Padding.Right;
                thickness = control.BorderThickness.Left + control.BorderThickness.Right;
            }

            double layer = margin + padding + thickness;
            chrome += layer;

            current = VisualTreeHelper.GetParent(current);
        }

        return double.NaN;
    }
}
