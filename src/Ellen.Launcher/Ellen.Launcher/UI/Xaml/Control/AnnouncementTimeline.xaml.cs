// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Ellen.Launcher.Core.Caching;
using Ellen.Launcher.Core.Graphics.Imaging;
using Ellen.Launcher.UI.Content;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Common.Announcement;
using System.Collections;
using System.Collections.Immutable;
using Windows.System;
using Windows.UI;
using WinUITextBlock = Microsoft.UI.Xaml.Controls.TextBlock;

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

    /// <summary>
    /// 时间条的最小宽度。封面固定占 <see cref="CoverWidth"/>，还要留出标题和右侧剩余时间的空间，
    /// 否则短公告（一两天）的条会窄到只剩半张图。
    /// </summary>
    private const double MinBarWidth = 196;

    /// <summary>封面在条内占的宽度（与参考站一致）。</summary>
    private const double CoverWidth = 128;

    /// <summary>标题向左压进封面渐变的距离。</summary>
    private const double TitleOverlap = 24;

    private const double TitleRightMargin = 10;
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

        // 取色要拿 IImageCache，得等控件进了可视树（XamlRoot 就绪）才能取到服务
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartPaletteLoads();
        UpdateStickyPositions();
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
        UpdateStickyPositions();
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
        StartPaletteLoads();
        UpdateStickyPositions();
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
        UpdateStickyPositions();
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
    /// 卡片尺寸变化后重新摆放贴左组：第一次布局完成时标题宽度才可用。
    /// </summary>
    private void OnBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateStickyPositions();
    }

    /// <summary>
    /// 让每条公告的「封面 + 标题」在横向滚动时钉住视口最左侧（与参考站 <c>sticky left-0</c> 一致）。
    /// <para>
    /// 组的自然位置就是条的左端；条左端滚出视口后，把它整体右移 <c>horizontalOffset - barLeft</c>，
    /// 让它停在视口左边缘。右移量再按「可用列宽 - 组宽」夹一次，
    /// 所以它永远不会越过自己这条时间条、也不会压到右侧固定的剩余时间。
    /// </para>
    /// </summary>
    private void UpdateStickyPositions()
    {
        double viewport = Viewport.ActualWidth;
        if (viewport <= 0d)
        {
            return;
        }

        foreach (BarEntry entry in bars)
        {
            if (!TryResolveParts(entry))
            {
                continue;
            }

            BarParts parts = entry.Parts!;

            double cardLeft = Canvas.GetLeft(entry.Presenter);
            double hostWidth = parts.Host.ActualWidth;
            if (double.IsNaN(cardLeft) || hostWidth <= 0d)
            {
                continue;
            }

            // 剩余时间叠在条右端，给它留出宽度，贴左组和标题都不许越过它
            double timeReserve = parts.Time.Visibility == Visibility.Visible
                ? parts.Time.ActualWidth + TitleRightMargin
                : 0d;

            // 标题必须先被条宽约束住，长标题才会省略号截断，
            // 也才能保证「组宽 ≤ 条宽 - 时间宽度」，滑动范围有意义。
            double titleMax = Math.Max(0d, hostWidth - (parts.HasCover ? CoverWidth - TitleOverlap : 0d) - TitleRightMargin - timeReserve);
            if (Math.Abs(parts.Title.MaxWidth - titleMax) > 0.5d)
            {
                parts.Title.MaxWidth = titleMax;
            }

            if (parts.Group.RenderTransform is not TranslateTransform transform)
            {
                continue;
            }

            double maxSlide = Math.Max(0d, hostWidth - timeReserve - parts.Group.ActualWidth);
            transform.X = Math.Clamp(horizontalOffset - cardLeft, 0d, maxSlide);
        }
    }

    /// <summary>
    /// 首次把条模板里的元素抓出来并缓存（模板里的 x:Name 不在控件命名空间内，只能走视觉树）。
    /// </summary>
    private static bool TryResolveParts(BarEntry entry)
    {
        if (entry.Parts is not null)
        {
            return true;
        }

        ContentPresenter presenter = entry.Presenter;
        if (FindDescendant<Border>(presenter, "Bar") is not { } bar
            || FindDescendant<Grid>(presenter, "StickyHost") is not { } host
            || FindDescendant<Grid>(presenter, "StickyGroup") is not { } group
            || FindDescendant<WinUITextBlock>(presenter, "TitleText") is not { } title
            || FindDescendant<WinUITextBlock>(presenter, "TimeText") is not { } time)
        {
            return false;
        }

        Grid? coverHost = FindDescendant<Grid>(presenter, "CoverHost");
        Border? coverFade = FindDescendant<Border>(presenter, "CoverFade");
        GradientStop? fadeStop = (coverFade?.Background as LinearGradientBrush)?.GradientStops.LastOrDefault();

        bool hasCover = coverHost is not null && !string.IsNullOrWhiteSpace(entry.Item.Banner);
        if (!hasCover && coverHost is not null)
        {
            // 没有封面的公告直接让标题从条左端开始，不留一块空图位
            coverHost.Visibility = Visibility.Collapsed;
            title.Margin = new Thickness(TitleRightMargin, 0d, TitleRightMargin, 0d);
        }

        entry.Parts = new(bar, title, time, host, group, fadeStop, hasCover);

        // 先用兜底色上屏（取色还没回来时也有颜色），取色结果回来后再覆盖
        ApplyBarColor(entry, GetFallbackPalette(entry.Item));
        if (entry.Palette is { } palette)
        {
            ApplyBarColor(entry, palette);
        }

        return true;
    }

    private static void ApplyBarColor(BarEntry entry, CoverPalette palette)
    {
        if (entry.Parts is not { } parts)
        {
            return;
        }

        parts.Bar.Background = new SolidColorBrush(palette.BarColor);

        // 封面右侧那层「透明 → 条色」的渐变要跟着条色一起变，否则接缝会露出色差
        if (parts.FadeStop is { } fadeStop)
        {
            fadeStop.Color = palette.BarColor;
        }

        SolidColorBrush text = new(palette.TextColor);
        parts.Title.Foreground = text;
        parts.Time.Foreground = text;
    }

    /// <summary>
    /// 取色未完成（或取色失败）时的兜底色，参考站同样按类别给固定色。
    /// </summary>
    private static CoverPalette GetFallbackPalette(Announcement item)
    {
        Color color = item.Type switch
        {
            1 => Color.FromArgb(0xFF, 0x5B, 0x7C, 0xFA),
            2 => Color.FromArgb(0xFF, 0x22, 0xA6, 0xB3),
            3 => Color.FromArgb(0xFF, 0x8B, 0x5C, 0xF6),
            _ => Color.FromArgb(0xFF, 0x64, 0x74, 0x8B),
        };

        return new(color, Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
    }

    /// <summary>
    /// 给每条公告发起一次封面取色（同一 URL 在 <see cref="CoverPaletteService"/> 里常驻缓存）。
    /// </summary>
    private void StartPaletteLoads()
    {
        if (bars.Count is 0)
        {
            return;
        }

        if (XamlRoot?.XamlContext()?.ServiceProvider.GetRequiredService<IImageCache>() is not { } imageCache)
        {
            return;
        }

        foreach (BarEntry entry in bars)
        {
            if (entry.IsPaletteRequested)
            {
                continue;
            }

            entry.IsPaletteRequested = true;

            if (!Uri.TryCreate(entry.Item.Banner, UriKind.Absolute, out Uri? bannerUri))
            {
                continue;
            }

            LoadPaletteAsync(entry, bannerUri, imageCache).SafeForget();
        }
    }

    private async Task LoadPaletteAsync(BarEntry entry, Uri bannerUri, IImageCache imageCache)
    {
        CoverPalette? palette = await CoverPaletteService.GetAsync(bannerUri, imageCache);
        if (palette is not { } value || !bars.Contains(entry))
        {
            return;
        }

        entry.Palette = value;
        ApplyBarColor(entry, value);
    }

    private static T? FindDescendant<T>(DependencyObject root, string name)
        where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            if (FindDescendant<T>(child, name) is { } found)
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
        /// 模板实例里的元素。要等首次布局完成才能从视觉树里找到，所以缓存起来。
        /// </summary>
        public BarParts? Parts { get; set; }

        /// <summary>封面取色结果，可能比模板实例先到。</summary>
        public CoverPalette? Palette { get; set; }

        public bool IsPaletteRequested { get; set; }
    }

    /// <summary>
    /// 一条时间条里需要用代码改动的元素。
    /// </summary>
    private sealed record BarParts(
        Border Bar,
        WinUITextBlock Title,
        WinUITextBlock Time,
        Grid Host,
        Grid Group,
        GradientStop? FadeStop,
        bool HasCover);

    private sealed record DayCell(Border Cell, WinUITextBlock Label, DateTimeOffset Day);
}
