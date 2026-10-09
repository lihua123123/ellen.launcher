// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ellen.Launcher.Model.Formulas;
using System.Collections.Immutable;

namespace Ellen.Launcher.UI.Xaml.Control;

/// <summary>
/// 把一个 Markdown 表格渲染成 <see cref="Grid"/>（ellen-wiki 的公式页里有 3 张宽表）。
/// <para>
/// 单元格文本走 <see cref="GuideRichText.SourceProperty"/>，因此 <c>**加粗**</c>、
/// <c>&lt;span class="pyro"&gt;</c> 这类行内标记与正文表现一致。
/// 列宽一律用 <c>*</c>，让表格自适应可用宽度并自动换行（比固定宽度更抗窄窗口）。
/// </para>
/// <para>
/// 外观对齐应用内既有表格（<c>WikiAvatarPage</c> 的战斗备注表）：文字居中、
/// 只用行底分隔线、表头用次要色文字，不做盒式网格。
/// </para>
/// </summary>
[SuppressMessage("", "SH001")]
[DependencyProperty<FormulaBlock>("Block", PropertyChangedCallbackName = nameof(OnBlockChanged))]
public sealed partial class MarkdownTable : ContentControl
{
    private const string StrokeBrushKey = "ControlStrokeColorDefaultBrush";
    private const string HeaderForegroundKey = "TextFillColorSecondaryBrush";

    public MarkdownTable()
    {
        IsTabStop = false;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Top;
    }

    private static void OnBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownTable control)
        {
            control.Rebuild();
        }
    }

    private static Brush? ResolveBrush(string key)
    {
        return Application.Current is { Resources: { } resources } && resources.TryGetValue(key, out object? value)
            ? value as Brush
            : null;
    }

    private void Rebuild()
    {
        if (Block is not { Head.Length: > 0 } block)
        {
            Content = null;
            return;
        }

        int columnCount = block.Head.Length;
        foreach (ImmutableArray<string> row in block.Rows)
        {
            columnCount = Math.Max(columnCount, row.Length);
        }

        Brush? stroke = ResolveBrush(StrokeBrushKey);
        Brush? headerForeground = ResolveBrush(HeaderForegroundKey);

        Grid grid = new();

        for (int i = 0; i < columnCount; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        for (int column = 0; column < columnCount; column++)
        {
            string text = column < block.Head.Length ? block.Head[column] : string.Empty;
            Border cell = CreateCell(text, stroke, headerForeground, isHeader: true);
            Grid.SetColumn(cell, column);
            Grid.SetRow(cell, 0);
            grid.Children.Add(cell);
        }

        for (int row = 0; row < block.Rows.Length; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int column = 0; column < columnCount; column++)
            {
                string text = column < block.Rows[row].Length ? block.Rows[row][column] : string.Empty;
                Border cell = CreateCell(text, stroke, foreground: null, isHeader: false);
                Grid.SetColumn(cell, column);
                Grid.SetRow(cell, row + 1);
                grid.Children.Add(cell);
            }
        }

        Content = grid;
    }

    private static Border CreateCell(string text, Brush? stroke, Brush? foreground, bool isHeader)
    {
        // 注意：本命名空间下存在子命名空间 .TextBlock，直接写 TextBlock 会被解析成命名空间。
        Microsoft.UI.Xaml.Controls.TextBlock textBlock = new()
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        if (isHeader)
        {
            textBlock.FontWeight = FontWeights.SemiBold;
        }

        if (foreground is not null)
        {
            textBlock.Foreground = foreground;
        }

        GuideRichText.SetSource(textBlock, text);

        return new Border
        {
            BorderBrush = stroke,
            BorderThickness = new Thickness(0, 0, 0, 1),
            MinHeight = 40,
            Padding = new Thickness(12, 6, 12, 6),
            Child = textBlock,
        };
    }
}
