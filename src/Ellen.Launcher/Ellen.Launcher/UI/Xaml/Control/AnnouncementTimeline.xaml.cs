// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Common.Announcement;
using System.Collections;
using System.Collections.Immutable;
using Windows.System;
using WinUITextBlock = Microsoft.UI.Xaml.Controls.TextBlock;
using Windows.Foundation;

namespace Ellen.Launcher.UI.Xaml.Control;

/// <summary>
/// 把公告按「起止时间」铺在横向时间轴上，并带一条可放缩的日期刻度。
/// <para>
/// 横向位移刻意不用内层 <c>ScrollViewer</c>，而是用 <c>TranslateTransform</c>：
/// 内层滚动容器会把滚轮事件吃掉，页面往下滚之后就没法用滚轮回到上面。
/// 这里普通滚轮完全不处理（交给页面纵向滚动），只有 Shift + 滚轮（或鼠标倾斜滚轮）
/// 才会横向移动时间轴；刻度长度用「＋ / －」放缩。
/// </para>
/// </summary>
[DependencyProperty<IEnumerable>("ItemsSource", PropertyChangedCallbackName = nameof(OnItemsSourceChanged))]
public sealed partial class AnnouncementTimeline : UserControl
{
    private const double RowHeight = 58;
    private const double RowGap = 8;
    private const double BarHeight = 48;
    private const double MinBarWidth = 140;
    private const double MinDayWidth = 8;
    private const double MaxDayWidth = 160;
    private const double DefaultDayWidth = 46;
    private const double ZoomFactor = 1.4;
    private const int MaxDays = 400;
    private const double WideDayWidth = 64;

    /// <summary>
    /// Shift + 滚轮时，滚轮一格（约 120）对应的横向像素倍数。
    /// </summary>
    private const double WheelScrollFactor = 0.8;

    private static readonly Brush GridLineBrush = new SolidColorBrush(new Windows.UI.Color { A = 0x33, R = 0x80, G = 0x80, B = 0x80 });

    private readonly List<BarEntry> bars = [];
    private readonly List<DayCell> dayCells = [];

    private DateTimeOffset rangeStart;
    private int totalDays = 1;
    private double dayWidth = DefaultDayWidth;
    private double horizontalOffset;
    private bool centeredOnToday;

    public AnnouncementTimeline()
    {
        InitializeComponent();

        // 用 handledEventsToo 挂到最外层，确保即使内部元素先处理过也能收到
        AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnTimelinePointerWheelChanged), true);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AnnouncementTimeline)d).Rebuild();
    }

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        Zoom(ZoomFactor);
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        Zoom(1d / ZoomFactor);
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Layout();
        CenterOnToday();
        UpdateTitlePositions();
    }

    /// <summary>
    /// 普通滚轮一律不处理，让页面自己纵向滚动；
    /// 只有 Shift + 滚轮（或鼠标倾斜滚轮）才横向移动时间轴。
    /// </summary>
    private void OnTimelinePointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        if (delta is 0)
        {
            return;
        }

        bool horizontal = e.GetCurrentPoint(this).Properties.IsHorizontalMouseWheel
            || (e.KeyModifiers & VirtualKeyModifiers.Shift) == VirtualKeyModifiers.Shift;

        if (!horizontal)
        {
            return;
        }

        SetHorizontalOffset(horizontalOffset - (delta * WheelScrollFactor));
        e.Handled = true;
    }

    private void Rebuild()
    {
        ScaleLayer.Children.Clear();
        BarLayer.Children.Clear();
        bars.Clear();
        dayCells.Clear();
        centeredOnToday = false;
        horizontalOffset = 0d;

        ImmutableArray<Announcement> items = [.. (ItemsSource ?? Array.Empty<object>()).OfType<Announcement>()];
        if (items.IsDefaultOrEmpty)
        {
            TodayText.Text = string.Empty;
            RangeText.Text = string.Empty;
            ScaleLayer.Width = 0;
            BarLayer.Width = 0;
            BarLayer.Height = 0;
            TodayLayer.Height = 0;
            return;
        }

        // 刻度按「天」对齐：起止都取当天零点
        DateTimeOffset minStart = items.Min(static item => item.StartTime).ToLocalTime();
        DateTimeOffset maxEnd = items.Max(static item => item.EndTime).ToLocalTime();
        DateTimeOffset earliestDay = new(minStart.Date, minStart.Offset);
        DateTimeOffset latestDay = new(maxEnd.Date, maxEnd.Offset);

        // 公告跨度可能横跨数年（部分公告的结束时间在 2035 年），远超可绘制宽度。
        // 因此不能像原来那样「从最早一条开始、截断到 MaxDays」——那样今天的公告会全部落到画布外，
        // 只剩最早那几天开始的三两条。跨度装不下时改为以「今天」为中心的 MaxDays 窗口。
        int span = (latestDay - earliestDay).Days + 1;
        if (span <= MaxDays)
        {
            rangeStart = earliestDay;
            totalDays = Math.Max(span, 1);
        }
        else
        {
            DateTimeOffset today = DateTimeOffset.Now;
            rangeStart = new DateTimeOffset(today.Date.AddDays(-(MaxDays / 2)), today.Offset);
            totalDays = MaxDays;
        }

        DateTimeOffset windowEnd = rangeStart.AddDays(totalDays);

        // 窗口之外的公告（早已结束、或尚未开始）不画，否则会全部叠在画布左端。
        ImmutableArray<Announcement> visible = [.. items.Where(item =>
            item.EndTime.ToLocalTime() >= rangeStart && item.StartTime.ToLocalTime() < windowEnd)];

        TodayText.Text = $"今天 {DateTimeOffset.Now:MM.dd}";
        RangeText.Text = $"{rangeStart:yyyy.MM.dd} - {rangeStart.AddDays(totalDays - 1):yyyy.MM.dd}";

        BuildDayCells();
        BuildBars(visible);
        Layout();
        CenterOnToday();
    }

    private void BuildDayCells()
    {
        for (int i = 0; i < totalDays; i++)
        {
            DateTimeOffset day = rangeStart.AddDays(i);
            bool isToday = day.Date == DateTimeOffset.Now.Date;

            WinUITextBlock label = new()
            {
                Text = FormatDay(day),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal,
            };

            Border cell = new()
            {
                Width = dayWidth,
                Height = 38,
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = GridLineBrush,
                Child = label,
            };

            dayCells.Add(new DayCell(cell, label, day));
            ScaleLayer.Children.Add(cell);
        }
    }

    private string FormatDay(DateTimeOffset day)
    {
        // 刻度窄的时候只显示日期，宽了以后补上月
        if (dayWidth >= WideDayWidth)
        {
            return $"{day.Month}/{day.Day}";
        }

        return day.Day == 1 ? $"{day.Month}/1" : day.Day.ToString();
    }

    private void BuildBars(ImmutableArray<Announcement> items)
    {
        if (Resources["AnnouncementTimelineBarTemplate"] is not DataTemplate template)
        {
            return;
        }

        foreach (Announcement item in items)
        {
            ContentPresenter presenter = new()
            {
                Content = item,
                ContentTemplate = template,
                Height = BarHeight,
            };

            bars.Add(new BarEntry(item, presenter));
            BarLayer.Children.Add(presenter);
        }
    }

    private void Zoom(double factor)
    {
        double previousDayWidth = dayWidth;
        dayWidth = Math.Clamp(dayWidth * factor, MinDayWidth, MaxDayWidth);
        if (Math.Abs(dayWidth - previousDayWidth) < 0.01)
        {
            return;
        }

        // 记住视口中心对应的时间，放缩后回到同一时刻，避免内容乱跳
        double viewport = Viewport.ActualWidth;
        double centerDays = previousDayWidth <= 0
            ? 0d
            : (horizontalOffset + (viewport / 2)) / previousDayWidth;

        foreach (DayCell cell in dayCells)
        {
            cell.Label.Text = FormatDay(cell.Day);
        }

        Layout();
        SetHorizontalOffset((centerDays * dayWidth) - (viewport / 2));
    }

    private void Layout()
    {
        double totalWidth = totalDays * dayWidth;
        ScaleLayer.Width = totalWidth;
        BarLayer.Width = totalWidth;

        for (int i = 0; i < dayCells.Count; i++)
        {
            dayCells[i].Cell.Width = dayWidth;
            Canvas.SetLeft(dayCells[i].Cell, i * dayWidth);
        }

        for (int i = 0; i < bars.Count; i++)
        {
            BarEntry entry = bars[i];
            double left = Math.Max(0d, (entry.Item.StartTime.ToLocalTime() - rangeStart).TotalDays * dayWidth);
            double duration = (entry.Item.EndTime - entry.Item.StartTime).TotalDays;
            double width = Math.Max(MinBarWidth, duration * dayWidth);
            width = Math.Min(width, Math.Max(MinBarWidth, totalWidth - left));

            Canvas.SetLeft(entry.Presenter, left);
            Canvas.SetTop(entry.Presenter, i * (RowHeight + RowGap));
            entry.Presenter.Width = width;
        }

        double totalHeight = bars.Count * (RowHeight + RowGap);
        BarLayer.Height = totalHeight;
        TodayLayer.Height = totalHeight;
        TodayLine.Height = totalHeight;
        Canvas.SetLeft(TodayLine, Math.Clamp((DateTimeOffset.Now - rangeStart).TotalDays * dayWidth, 0d, totalWidth));

        SetHorizontalOffset(horizontalOffset);
    }

    private void SetHorizontalOffset(double value)
    {
        double viewport = Viewport.ActualWidth;
        double totalWidth = totalDays * dayWidth;
        double max = Math.Max(0d, totalWidth - viewport);

        horizontalOffset = Math.Clamp(value, 0d, max);
        HorizontalOffsetTransform.X = -horizontalOffset;
        UpdateTitlePositions();
    }

    private void CenterOnToday()
    {
        if (centeredOnToday || Viewport.ActualWidth <= 0)
        {
            return;
        }

        double todayLeft = (DateTimeOffset.Now - rangeStart).TotalDays * dayWidth;
        if (todayLeft < 0 || todayLeft > totalDays * dayWidth)
        {
            return;
        }

        centeredOnToday = true;
        SetHorizontalOffset(todayLeft - (Viewport.ActualWidth / 2));
    }

    /// <summary>
    /// 卡片尺寸变化后重新摆放标题：第一次布局完成时标题宽度才可用。
    /// </summary>
    private void OnBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTitlePositions();
    }

    /// <summary>
    /// 让每条公告卡片里的标题主动贴近视口中轴，但不越过卡片边框。
    /// <para>
    /// 标题所在的那一列就是它可用的全部空间：目标位置是把文字中心对到视口中轴，
    /// 然后拿「列范围」夹取一次，所以最多滑到列边上，永远不会越出卡片。
    /// 列宽装不下文字时（窄卡片）可滑距离为 0，保持原样。
    /// </para>
    /// </summary>
    private void UpdateTitlePositions()
    {
        double viewport = Viewport.ActualWidth;
        if (viewport <= 0d)
        {
            return;
        }

        double centerX = viewport / 2d;
        foreach (BarEntry entry in bars)
        {
            double cardLeft = Canvas.GetLeft(entry.Presenter);
            if (double.IsNaN(cardLeft))
            {
                continue;
            }

            WinUITextBlock? title = entry.Title;
            if (title is null || title.XamlRoot is null)
            {
                title = FindTitleText(entry.Presenter);
                entry.Title = title;
            }

            if (title?.RenderTransform is not TranslateTransform transform)
            {
                continue;
            }

            double titleWidth = title.ActualWidth;
            if (titleWidth <= 0d)
            {
                continue;
            }

            Rect slot = LayoutInformation.GetLayoutSlot(title);
            double slideRange = slot.Width - titleWidth;
            if (slideRange <= 1d)
            {
                transform.X = 0d;
                continue;
            }

            double cardLeftInViewport = cardLeft - horizontalOffset;
            double desired = centerX - cardLeftInViewport - slot.X - (titleWidth / 2d);
            transform.X = Math.Clamp(desired, 0d, slideRange);
        }
    }

    private static WinUITextBlock? FindTitleText(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is WinUITextBlock { Name: "TitleText" } title)
            {
                return title;
            }

            if (FindTitleText(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private sealed class BarEntry(Announcement item, ContentPresenter presenter)
    {
        public Announcement Item { get; } = item;

        public ContentPresenter Presenter { get; } = presenter;

        /// <summary>
        /// 模板实例里的标题。要等首次布局完成才能从视觉树里找到，所以缓存起来。
        /// </summary>
        public WinUITextBlock? Title { get; set; }
    }

    private sealed record DayCell(Border Cell, WinUITextBlock Label, DateTimeOffset Day);
}
