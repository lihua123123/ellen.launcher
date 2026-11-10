// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Frozen;
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
/// <c>`专有名词`</c> 沿用软件默认字体，<c>$公式/数值$</c> 用随包内置的 KaTeX 公式字体
/// （WinUI 没有 KaTeX，不做真正的公式排版），
/// <c>**加粗**</c> 直接加粗；其余 HTML 标签一律丢弃并保留其中文字。
/// </para>
/// <para>
/// 另提供 <see cref="AutoElementColorProperty"/>：开启后会扫描纯文本里的元素/反应关键词
/// （<c>火</c>、<c>月感电</c>、<c>星超导</c> 等）自动上色，无需在数据里手写 span。
/// </para>
/// </summary>
public static class GuideRichText
{
    // 元素/反应配色与关键词表已抽到 ElementPalette（内置 colors.json，供后续功能复用）。

    private static readonly Regex TokenRegex = new(
        "`(?<code>[^`]+)`"
        + "|\\$(?<math>[^$]+)\\$"
        + "|\\*\\*(?<strong>.+?)\\*\\*"
        + "|(?<spanOpen><span\\s+class=\"(?<cls>[^\"]+)\"\\s*>)"
        + "|(?<spanClose></span\\s*>)"
        // 敌人图鉴技能描述用的是行内颜色标记（与 ellen-wiki richtext.js 同源）：
        // <c=#FF9999>火</c> / <color=#FF9999>火</color>。必须放在通用 tag 之前，否则会被当 HTML 丢掉。
        + "|(?<cOpen><(?:c|color)=(?<hex>#[0-9a-fA-F]{6})>)"
        + "|(?<cClose></(?:c|color)>)"
        + "|(?<tag><[^>]+>)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private const int MaxTokenDepth = 4;

    /// <summary>
    /// 无元素类型的盾（次数盾 / 生命值盾）回退用的物理配色，与 <see cref="ElementPalette"/>
    /// 里的 <c>physical</c> 保持一致（正常路径不会用到，只防配色表改动）。
    /// </summary>
    private static readonly Color StatFallbackColor = new() { A = 0xFF, R = 0xB8, G = 0xB8, B = 0xB8 };

    /// <summary>护盾正文里允许单字着色的元素字（含「冻」这种写法）。</summary>
    private const string ElementChars = "火水草雷风冰岩冻";

    /// <summary>护盾正文里的元素单字 → 配色键（「冻」是冰盾的另一种写法）。</summary>
    private static readonly FrozenDictionary<char, string> ElementKeys = new Dictionary<char, string>
    {
        ['火'] = "pyro",
        ['水'] = "hydro",
        ['草'] = "dendro",
        ['雷'] = "electro",
        ['风'] = "anemo",
        ['冰'] = "cryo",
        ['岩'] = "geo",
        ['冻'] = "cryo",
    }.ToFrozenDictionary();

    private static FontFamily? mathFontFamily;

    /// <summary>
    /// 行内公式用的字体：ellen-wiki 用 KaTeX 渲染公式（<c>katex/dist/katex.min.css</c> 里的
    /// <c>KaTeX_Main</c>），这里把同一套字体随包内置并直接用在 <c>$...$</c> 上，
    /// 让行内公式看起来是公式；中文等 KaTeX 没有的字形由 MiSans 兜底。
    /// </summary>
    private static FontFamily? MathFontFamily
    {
        get
        {
            if (mathFontFamily is null
                && Application.Current?.Resources.TryGetValue("KaTeXMainAndMiSans", out object? value) is true
                && value is FontFamily family)
            {
                mathFontFamily = family;
            }

            return mathFontFamily;
        }
    }

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
    /// 是否整段加粗（默认关闭）。开启后未带 <c>**</c> 的正文也按半粗渲染，
    /// 用于护盾这类「整块都是重点」的短文本。
    /// </summary>
    public static readonly DependencyProperty BoldProperty = DependencyProperty.RegisterAttached(
        "Bold",
        typeof(bool),
        typeof(GuideRichText),
        new PropertyMetadata(false, OnRenderOptionChanged));

    public static bool GetBold(DependencyObject element)
    {
        return (bool)element.GetValue(BoldProperty);
    }

    public static void SetBold(DependencyObject element, bool value)
    {
        element.SetValue(BoldProperty, value);
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
        OnRenderOptionChanged(d, e);
    }

    /// <summary>
    /// 数值染色开关（护盾正文、怪物技能、特殊机制共用一套规则）：
    /// 盾量表达式（<c>2草盾</c> / <c>12盾量</c>）整体上色；
    /// 带单位的数字（<c>10%</c> / <c>90s</c> / <c>3min</c>）连单位一起斜体、不上色；
    /// 其余数字保持正文色。
    /// </summary>
    public static readonly DependencyProperty StatColorProperty = DependencyProperty.RegisterAttached(
        "StatColor",
        typeof(bool),
        typeof(GuideRichText),
        new PropertyMetadata(false, OnRenderOptionChanged));

    public static bool GetStatColor(DependencyObject element)
    {
        return (bool)element.GetValue(StatColorProperty);
    }

    public static void SetStatColor(DependencyObject element, bool value)
    {
        element.SetValue(StatColorProperty, value);
    }

    /// <summary>
    /// 数值染色用的元素键（<c>pyro</c> / <c>dendro</c> …）。
    /// 护盾没写元素、或技能正文不绑定时留空，盾量表达式改用物理配色（中性灰）。
    /// </summary>
    public static readonly DependencyProperty StatElementProperty = DependencyProperty.RegisterAttached(
        "StatElement",
        typeof(string),
        typeof(GuideRichText),
        new PropertyMetadata(null, OnRenderOptionChanged));

    public static string? GetStatElement(DependencyObject element)
    {
        return (string?)element.GetValue(StatElementProperty);
    }

    public static void SetStatElement(DependencyObject element, string? value)
    {
        element.SetValue(StatElementProperty, value);
    }

    private static void OnRenderOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
        textBlock.Inlines.Clear();
        if (source.Length is 0)
        {
            return;
        }

        // 「50%,50%,60%」这类倍率 / 数值枚举里的英文逗号统一成中文逗号
        source = NormalizeNumericComma(source);

        Brush? statBrush = null;
        if (GetStatColor(textBlock))
        {
            if (GetStatElement(textBlock) is { Length: > 0 } key
                && ElementPalette.TryGetBrush(key, out Brush? elementBrush))
            {
                // 声明了元素：盾量表达式统一走该元素色
                statBrush = elementBrush;
            }
            else
            {
                // 没声明元素（次数盾 / 生命值盾 / 技能正文）：按物理配色（中性灰）染盾量
                statBrush = ElementPalette.TryGetBrush("physical", out Brush? physical)
                    ? physical
                    : new SolidColorBrush(StatFallbackColor);
            }
        }

        AppendTokens(
            textBlock,
            source,
            null,
            GetBold(textBlock) ? FontWeights.SemiBold : null,
            GetAutoElementColor(textBlock),
            statBrush,
            0);
    }

    /// <summary>
    /// 单趟扫描整段文本：<c>**加粗**</c> 可以跨越 <c>&lt;span&gt;</c> 标签（wiki 里
    /// <c>**反应&lt;span class="x"&gt;月感电&lt;/span&gt;**</c> 这种写法很常见），
    /// 元素标签用栈维护配色，因此跨标签的标记也不会漏成字面文本。
    /// </summary>
    private static bool AppendTokens(WinUITextBlock textBlock, string text, Brush? brush, FontWeight? weight, bool autoColor, Brush? statBrush, int depth, char previous = '\0')
    {
        if (depth > MaxTokenDepth)
        {
            return AddRun(textBlock, text, brush, weight, null, autoColor, statBrush, previous);
        }

        Stack<Brush?> brushStack = new();
        int cursor = 0;

        // 上一段以斜体数值结尾（`**100%**伤害`），下一段开头补一个空格
        bool padNext = false;

        // 上一个**可见**字符：用来判断「盾」是不是紧跟在「20岩」后面。
        // 加粗标记会把 `**20岩**盾` 切成两段，只看本段是看不到前面那个「岩」的。
        char tail = previous;

        foreach (Match match in TokenRegex.Matches(text))
        {
            if (match.Index > cursor)
            {
                string chunk = text[cursor..match.Index];
                padNext = AddRun(textBlock, Pad(padNext, chunk), brush, weight, null, autoColor, statBrush, tail);
                tail = chunk[^1];
            }

            if (match.Groups["strong"].Success)
            {
                // 加粗内部可能还嵌着元素标签，递归处理
                string strong = match.Groups["strong"].Value;
                padNext = AppendTokens(textBlock, strong, brush, FontWeights.SemiBold, autoColor, statBrush, depth + 1, tail);
                tail = LastVisibleChar(strong, tail);
            }
            else if (match.Groups["code"].Success)
            {
                string code = match.Groups["code"].Value;
                padNext = AddRun(textBlock, Pad(padNext, code), brush, weight, null, autoColor, statBrush, tail);
                tail = code[^1];
            }
            else if (match.Groups["math"].Success)
            {
                string math = CleanMath(match.Groups["math"].Value);
                padNext = AddRun(textBlock, Pad(padNext, math), brush, weight, MathFontFamily, autoColor, statBrush, tail);
                if (math.Length > 0)
                {
                    tail = math[^1];
                }
            }
            else if (match.Groups["spanOpen"].Success)
            {
                brushStack.Push(brush);
                if (ElementPalette.TryGetBrush(match.Groups["cls"].Value, out Brush? spanBrush))
                {
                    brush = spanBrush;
                }
            }
            else if (match.Groups["spanClose"].Success && brushStack.Count > 0)
            {
                brush = brushStack.Pop();
            }
            else if (match.Groups["cOpen"].Success)
            {
                brushStack.Push(brush);
                if (ElementPalette.BrushFromHex(match.Groups["hex"].Value) is { } hexBrush)
                {
                    brush = hexBrush;
                }
            }
            else if (match.Groups["cClose"].Success && brushStack.Count > 0)
            {
                brush = brushStack.Pop();
            }

            // 其余标签（tag 组）直接丢弃
            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            string rest = text[cursor..];
            padNext = AddRun(textBlock, Pad(padNext, rest), brush, weight, null, autoColor, statBrush, tail);
        }

        return padNext;
    }

    /// <summary>上一段以斜体数值结尾时，给下一段开头补一个空格（已是空白就不重复补）。</summary>
    private static string Pad(bool pad, string text)
    {
        return pad && text.Length > 0 && !char.IsWhiteSpace(text[0]) ? " " + text : text;
    }

    /// <summary>取一段标记文本里最后一个可见字符（末尾是 <c>&gt;</c> 说明还挂着标签，沿用上一个）。</summary>
    private static char LastVisibleChar(string marked, char fallback)
    {
        return marked.Length > 0 && marked[^1] != '>' ? marked[^1] : fallback;
    }

    /// <summary>追加一段 Run，返回「这段是否以斜体数值结尾」（下一段要补前空格）。</summary>
    private static bool AddRun(WinUITextBlock textBlock, string text, Brush? brush, FontWeight? weight, FontFamily? font, bool autoColor, Brush? statBrush, char previous = '\0')
    {
        if (text.Length is 0)
        {
            return false;
        }

        if (brush is null && (autoColor || statBrush is not null))
        {
            return AddAutoColoredRuns(textBlock, text, weight, font, statBrush, previous);
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
        return false;
    }

    /// <summary>
    /// 把一段纯文本按元素/反应关键词切成多段，命中的关键词走元素配色。
    /// <para>
    /// 开了 <paramref name="statBrush"/>（护盾 / 技能正文的数值染色）后额外分三类：
    /// ① 盾量表达式（<c>2草盾</c> / <c>12盾量</c> / <c>20岩</c>）整体上色，有元素字时用该元素色；
    /// ② 带单位的小数值（<c>10%</c> / <c>90s</c> / <c>3min</c> / <c>5秒</c>）连同**单位一起**斜体，不上色；
    /// ③ 其余数字保持正文色（「抗打断0」这类数值不再被点亮）。
    /// </para>
    /// <para>
    /// 「盾」只在紧跟数字或元素字时上色，所以「生命值盾」的「盾」保持正文色；
    /// <c>**20岩**盾</c> 被加粗切成两段时，靠 <paramref name="previous"/> 把上一段末尾的「岩」带过来。
    /// </para>
    /// </summary>
    private static bool AddAutoColoredRuns(WinUITextBlock textBlock, string text, FontWeight? weight, FontFamily? font, Brush? statBrush, char previous)
    {
        int start = 0;
        int index = 0;
        bool endsWithItalic = false;

        void Flush(int to)
        {
            if (to > start)
            {
                AddRun(textBlock, text[start..to], null, weight, font, false, null);
                endsWithItalic = false;
            }
        }

        void Emit(int from, int to, Brush? brush, Windows.UI.Text.FontStyle? style = null, bool pad = false)
        {
            Flush(from);

            // 斜体数值前后各留一个空格（「持续 90s 。」，避免和中文糊在一起）。
            // 紧挨标点或已有空白时不补，否则会出现「( 1% )」「20% ，」这类多余间隙。
            string content = text[from..to];
            if (pad)
            {
                char before = from > 0 ? text[from - 1] : previous;
                if ((from > 0 || previous != '\0') && NeedsPadSpace(before))
                {
                    content = " " + content;
                }

                if (to < text.Length && NeedsPadSpace(text[to]))
                {
                    content += " ";
                }
            }

            Run run = new() { Text = content };
            if (brush is not null)
            {
                run.Foreground = brush;
            }

            if (style is { } value)
            {
                run.FontStyle = value;
            }

            if (weight is { } fontWeight)
            {
                run.FontWeight = fontWeight;
            }

            if (font is not null)
            {
                run.FontFamily = font;
            }

            textBlock.Inlines.Add(run);
            index = to;
            start = to;
            endsWithItalic = pad;
        }

        while (index < text.Length)
        {
            // ① 盾量表达式 / ② 百分比与时长
            if (statBrush is not null && char.IsAsciiDigit(text[index]))
            {
                int numberEnd = ScanNumber(text, index);

                // 数字后面紧跟元素字才算盾量；「2水元素」的「水」不属于盾量，交给元素整词着色
                bool hasElement = numberEnd < text.Length
                    && ElementChars.Contains(text[numberEnd])
                    && (numberEnd + 1 >= text.Length || text[numberEnd + 1] != '元');

                int end = numberEnd + (hasElement ? 1 : 0);
                bool hasShield = end < text.Length && text[end] == '盾';
                if (hasShield)
                {
                    end++;
                    // 「12盾量」连「量」一起染
                    if (end < text.Length && text[end] == '量')
                    {
                        end++;
                    }
                }

                if (hasElement || hasShield)
                {
                    Emit(index, end, hasElement ? ResolveElementBrush(text[numberEnd]) ?? statBrush : statBrush);
                    continue;
                }

                // 「10%」「90s」「3min」：数字与单位同属斜体，否则单位会留在正体里断开
                int unitEnd = ScanUnit(text, numberEnd);
                if (unitEnd > numberEnd)
                {
                    Emit(index, unitEnd, null, Windows.UI.Text.FontStyle.Italic, pad: true);
                    continue;
                }

                // ③ 与其无关的数字：原样留给后面的整段输出
                index = numberEnd;
                continue;
            }

            // 「盾」：只有紧跟数字（12盾）或元素字（草盾）时才染色，「生命值盾」的「盾」不动
            if (statBrush is not null && text[index] == '盾')
            {
                char before = index > 0 ? text[index - 1] : previous;
                if (char.IsAsciiDigit(before) || ElementChars.Contains(before))
                {
                    Emit(index, index + 1, statBrush);
                    continue;
                }
            }

            if (ElementPalette.TryMatchKeyword(text, index, out string key, out int length)
                && ElementPalette.TryGetBrush(key, out Brush? brush))
            {
                Emit(index, index + length, brush);
                continue;
            }

            if (statBrush is not null && ElementChars.Contains(text[index]))
            {
                // 「草盾」这种没写数字的盾量
                if (index + 1 < text.Length && text[index + 1] == '盾')
                {
                    Emit(index, index + 2, ResolveElementBrush(text[index]) ?? statBrush);
                    continue;
                }

                // 「火附魔」：只点亮元素字，附魔保持正文色
                if (index + 3 <= text.Length
                    && string.CompareOrdinal(text, index + 1, "附魔", 0, 2) == 0
                    && ResolveElementBrush(text[index]) is { } attachBrush)
                {
                    Emit(index, index + 1, attachBrush);
                    continue;
                }
            }

            index++;
        }

        Flush(text.Length);
        return endsWithItalic;
    }

    /// <summary>
    /// 斜体数值补空格用的判断：相邻是空白 / 标点 / 符号（<c>%</c> <c>+</c> <c>(</c> <c>。</c> <c>，</c>）时不补，
    /// 相邻是汉字或字母时补一个，免得「持续90s」糊成一团。
    /// </summary>
    private static bool NeedsPadSpace(char neighbour)
    {
        return neighbour != '\0'
            && !char.IsWhiteSpace(neighbour)
            && !char.IsPunctuation(neighbour)
            && !char.IsSymbol(neighbour);
    }

    /// <summary>
    /// 把「数字与数字之间」的英文逗号换成中文逗号：「50%,50%,60%」→「50%，50%，60%」，
    /// 「(1,0.5)」→「(1，0.5)」。逗号后必须是数字；逗号前允许夹一个单位（<c>%</c> / <c>s</c> / <c>min</c> / <c>秒</c>），
    /// 这样「20%,30%」也能命中，而英文句子里的逗号不会被改。
    /// </summary>
    private static string NormalizeNumericComma(string text)
    {
        if (!text.Contains(','))
        {
            return text;
        }

        char[]? buffer = null;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == ',' && IsNumericComma(text, i))
            {
                buffer ??= text.ToCharArray();
                buffer[i] = '，';
            }
        }

        return buffer is null ? text : new string(buffer);
    }

    private static bool IsNumericComma(string text, int commaIndex)
    {
        // 右侧：跳过空白（含数据里的硬换行）后必须直接是数字
        int next = commaIndex + 1;
        while (next < text.Length && char.IsWhiteSpace(text[next]))
        {
            next++;
        }

        if (next >= text.Length || !char.IsAsciiDigit(text[next]))
        {
            return false;
        }

        // 左侧：允许先跳过单位（「20%」的「%」、「90s」的「s」、「3min」、「5秒」）
        int prev = commaIndex - 1;
        bool bareDigit = prev >= 0 && char.IsAsciiDigit(text[prev]);

        while (prev >= 0 && text[prev] is '%' or 's' or 'S' or '秒')
        {
            prev--;
        }

        if (prev >= 2 && string.CompareOrdinal(text, prev - 2, "min", 0, 3) == 0)
        {
            prev -= 3;
        }

        if (prev < 0 || !char.IsAsciiDigit(text[prev]))
        {
            return false;
        }

        // 千分位（1,000 / 12,500）不是枚举，保持英文逗号；
        // 带单位的（50%,500%）左边不是裸数字，不受这条限制
        if (bareDigit
            && next + 2 < text.Length
            && char.IsAsciiDigit(text[next + 1])
            && char.IsAsciiDigit(text[next + 2])
            && (next + 3 >= text.Length || !char.IsAsciiDigit(text[next + 3])))
        {
            return false;
        }

        return true;
    }

    /// <summary>扫一个数字（允许 <c>0.8</c> 这种单个小数点），返回第一个非数字字符的下标。</summary>
    private static int ScanNumber(string text, int start)
    {
        int index = start;
        bool hasDot = false;
        while (index < text.Length)
        {
            if (char.IsAsciiDigit(text[index]))
            {
                index++;
            }
            else if (text[index] == '.' && !hasDot && index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]))
            {
                hasDot = true;
                index++;
            }
            else
            {
                break;
            }
        }

        return index;
    }

    /// <summary>
    /// 数字后面单位的结束下标（<c>%</c> / <c>s</c> / <c>min</c> / <c>秒</c>）；没有单位时原样返回
    /// <paramref name="index"/>。单位要和数字一起进同一个 Run，否则只有数字是斜体、符号还是正的。
    /// </summary>
    private static int ScanUnit(string text, int index)
    {
        if (index >= text.Length)
        {
            return index;
        }

        char unit = text[index];
        if (unit is '%' or 's' or 'S' or '秒')
        {
            return index + 1;
        }

        return index + 3 <= text.Length && string.CompareOrdinal(text, index, "min", 0, 3) == 0
            ? index + 3
            : index;
    }

    /// <summary>护盾正文里的元素单字 → 该元素的配色（「冻」按冰算）。</summary>
    private static Brush? ResolveElementBrush(char element)
    {
        return ElementKeys.TryGetValue(element, out string? key) && ElementPalette.TryGetBrush(key, out Brush? brush)
            ? brush
            : null;
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
