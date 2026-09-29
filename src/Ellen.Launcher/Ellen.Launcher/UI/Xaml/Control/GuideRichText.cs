// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Text.RegularExpressions;
using Windows.UI;
using Windows.UI.Text;
using WinUITextBlock = Microsoft.UI.Xaml.Controls.TextBlock;

namespace Ellen.Launcher.UI.Xaml.Control;

/// <summary>
/// 给 <see cref="WinUITextBlock"/> 附加一个富文本源，把幽境攻略的行内标记渲染成样式化文本。
/// <para>
/// 标记与 ellen-wiki 的 <c>richtext.js</c> 对齐：
/// <c>&lt;span class="hydro"&gt;**水盾**&lt;/span&gt;</c> 走元素配色（配色表见 <see cref="ElementPalette"/>），
/// <c>`专有名词`</c> 与 <c>$公式/数值$</c> 用等宽字体（WinUI 没有 KaTeX，不做真正的公式排版），
/// <c>**加粗**</c> 直接加粗；其余 HTML 标签一律丢弃并保留其中文字。
/// </para>
/// <para>
/// 另提供 <see cref="AutoElementColorProperty"/>：开启后会扫描纯文本里的元素/反应关键词
/// （<c>火</c>、<c>月感电</c>、<c>星超导</c> 等）自动上色，无需在数据里手写 span。
/// </para>
/// </summary>
public static class GuideRichText
{
    private static readonly FontFamily MonospaceFont = new("Consolas");

    // 元素/反应配色与关键词表已抽到 ElementPalette（内置 colors.json，供后续功能复用）。

    private static readonly Regex ElementSpanRegex = new(
        "<span\\s+class=\"(?<cls>[^\"]+)\"\\s*>(?<inner>.*?)</span>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TokenRegex = new(
        "`(?<code>[^`]+)`|\\$(?<math>[^$]+)\\$|\\*\\*(?<strong>[^*]+)\\*\\*|(?<tag><[^>]+>)",
        RegexOptions.Compiled);

    /// <summary>
    /// 富文本源（保留标记的原始行）。
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached(
        "Source",
        typeof(string),
        typeof(GuideRichText),
        new PropertyMetadata(null, OnSourceChanged));

    public static string? GetSource(DependencyObject element)
    {
        return (string?)element.GetValue(SourceProperty);
    }

    public static void SetSource(DependencyObject element, string? value)
    {
        element.SetValue(SourceProperty, value);
    }

    /// <summary>
    /// 是否把正文里的元素/反应关键词自动着上元素配色（默认关闭）。
    /// </summary>
    public static readonly DependencyProperty AutoElementColorProperty = DependencyProperty.RegisterAttached(
        "AutoElementColor",
        typeof(bool),
        typeof(GuideRichText),
        new PropertyMetadata(false, OnAutoElementColorChanged));

    public static bool GetAutoElementColor(DependencyObject element)
    {
        return (bool)element.GetValue(AutoElementColorProperty);
    }

    public static void SetAutoElementColor(DependencyObject element, bool value)
    {
        element.SetValue(AutoElementColorProperty, value);
    }

    private static void OnAutoElementColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WinUITextBlock textBlock)
        {
            Render(textBlock, GetSource(textBlock) ?? string.Empty);
        }
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not WinUITextBlock textBlock)
        {
            return;
        }

        Render(textBlock, e.NewValue as string ?? string.Empty);
    }

    private static void Render(WinUITextBlock textBlock, string source)
    {
        bool autoColor = GetAutoElementColor(textBlock);

        textBlock.Inlines.Clear();
        if (source.Length is 0)
        {
            return;
        }

        int cursor = 0;
        foreach (Match span in ElementSpanRegex.Matches(source))
        {
            if (span.Index > cursor)
            {
                AppendTokens(textBlock, source[cursor..span.Index], null, autoColor);
            }

            string className = span.Groups["cls"].Value;
            ElementPalette.TryGetBrush(className, out Brush? brush);
            AppendTokens(textBlock, span.Groups["inner"].Value, brush, autoColor);

            cursor = span.Index + span.Length;
        }

        if (cursor < source.Length)
        {
            AppendTokens(textBlock, source[cursor..], null, autoColor);
        }
    }

    private static void AppendTokens(WinUITextBlock textBlock, string text, Brush? brush, bool autoColor)
    {
        int cursor = 0;
        foreach (Match match in TokenRegex.Matches(text))
        {
            if (match.Index > cursor)
            {
                AddRun(textBlock, text[cursor..match.Index], brush, null, null, autoColor);
            }

            if (match.Groups["strong"].Success)
            {
                AddRun(textBlock, match.Groups["strong"].Value, brush, FontWeights.SemiBold, null, autoColor);
            }
            else if (match.Groups["code"].Success)
            {
                AddRun(textBlock, match.Groups["code"].Value, brush, null, MonospaceFont, autoColor);
            }
            else if (match.Groups["math"].Success)
            {
                AddRun(textBlock, CleanMath(match.Groups["math"].Value), brush, null, MonospaceFont, autoColor);
            }

            // 其余标签（tag 组）直接丢弃
            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            AddRun(textBlock, text[cursor..], brush, null, null, autoColor);
        }
    }

    private static void AddRun(WinUITextBlock textBlock, string text, Brush? brush, FontWeight? weight, FontFamily? font, bool autoColor)
    {
        if (text.Length is 0)
        {
            return;
        }

        if (brush is null && autoColor)
        {
            AddAutoColoredRuns(textBlock, text, weight, font);
            return;
        }

        Run run = new() { Text = text };
        if (brush is not null)
        {
            run.Foreground = brush;
        }

        if (weight is { } value)
        {
            run.FontWeight = value;
        }

        if (font is not null)
        {
            run.FontFamily = font;
        }

        textBlock.Inlines.Add(run);
    }

    /// <summary>
    /// 把一段纯文本按元素/反应关键词切成多段，命中的关键词走元素配色。
    /// </summary>
    private static void AddAutoColoredRuns(WinUITextBlock textBlock, string text, FontWeight? weight, FontFamily? font)
    {
        int start = 0;
        int index = 0;

        while (index < text.Length)
        {
            if (ElementPalette.TryMatchKeyword(text, index, out string key, out int length)
                && ElementPalette.TryGetBrush(key, out Brush? brush))
            {
                if (index > start)
                {
                    AddRun(textBlock, text[start..index], null, weight, font, false);
                }

                AddRun(textBlock, text.Substring(index, length), brush, weight, font, false);
                index += length;
                start = index;
            }
            else
            {
                index++;
            }
        }

        if (start < text.Length)
        {
            AddRun(textBlock, text[start..], null, weight, font, false);
        }
    }

    /// <summary>
    /// 公式标记里保留正文写法（<c>12\\%</c> → <c>12%</c>），只去掉转义反斜杠。
    /// </summary>
    private static string CleanMath(string value)
    {
        return value
            .Replace("\\%", "%", StringComparison.Ordinal)
            .Replace("\\", string.Empty, StringComparison.Ordinal)
            .Trim();
    }
}
