// C2 富文本 → 安全 span。目标端 GuideRichText 只认 ** / ` / $ / <span class>，
// 不认 <c=#hex> 与 <color=>；这里统一解析成数据 span，绝不注入 HTML。

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class WikiTextSpan
{
    public string Text { get; init; } = string.Empty;

    /// <summary>"#RRGGBB" 或 "class:元素键"；null 表示不着色。</summary>
    public string? ColorHex { get; init; }

    public bool Bold { get; init; }
    public bool Code { get; init; }
    public bool Math { get; init; }

    public bool SameStyle(WikiTextSpan other)
    {
        return string.Equals(ColorHex, other.ColorHex, StringComparison.Ordinal)
            && Bold == other.Bold && Code == other.Code && Math == other.Math;
    }
}

public static class WikiRichText
{
    private static readonly Regex AnyTag = new(@"</?([a-zA-Z][a-zA-Z0-9]*)\b[^>]*>", RegexOptions.Compiled);
    private static readonly Regex Token = new(
        @"<c=#(?<hex>[0-9a-fA-F]{3,8})>|</c>|<color=#(?<hex2>[0-9a-fA-F]{3,8})>|</color>|<span\b[^>]*class=""(?<cls>[^""]+)""[^>]*>|</span\s*>|\*\*|`|\$",
        RegexOptions.Compiled);

    /// <summary>白名单外标签一律剥掉、保留文字；<c=#hex> / <color=> / <span class> / ** / ` / $ 解析为 span。</summary>
    public static IReadOnlyList<WikiTextSpan> Parse(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return [];
        }

        string text = AnyTag.Replace(input, match =>
        {
            string tag = match.Groups[1].Value.ToLowerInvariant();
            return tag switch
            {
                "c" or "color" or "span" => match.Value,
                "br" => "\n",
                _ => string.Empty,
            };
        });

        List<WikiTextSpan> spans = [];
        Stack<string?> colors = new();
        string? color = null;
        bool bold = false;
        bool code = false;
        bool math = false;
        int position = 0;

        void Append(string? chunk)
        {
            if (string.IsNullOrEmpty(chunk))
            {
                return;
            }

            WikiTextSpan span = new() { Text = chunk, ColorHex = color, Bold = bold, Code = code, Math = math };
            if (spans.Count > 0 && spans[^1].SameStyle(span))
            {
                WikiTextSpan previous = spans[^1];
                spans[^1] = new WikiTextSpan
                {
                    Text = previous.Text + chunk,
                    ColorHex = previous.ColorHex,
                    Bold = previous.Bold,
                    Code = previous.Code,
                    Math = previous.Math,
                };
            }
            else
            {
                spans.Add(span);
            }
        }

        foreach (Match match in Token.Matches(text))
        {
            Append(text[position..match.Index]);
            string value = match.Value;

            if (value.StartsWith("<c=#", StringComparison.Ordinal) || value.StartsWith("<color=#", StringComparison.Ordinal))
            {
                colors.Push(color);
                color = "#" + (match.Groups["hex"].Success ? match.Groups["hex"].Value : match.Groups["hex2"].Value);
            }
            else if (value.StartsWith("<span", StringComparison.Ordinal))
            {
                colors.Push(color);
                color = "class:" + match.Groups["cls"].Value;
            }
            else if (value.StartsWith("</", StringComparison.Ordinal))
            {
                color = colors.Count > 0 ? colors.Pop() : null;
            }
            else if (value == "**")
            {
                bold = !bold;
            }
            else if (value == "`")
            {
                code = !code;
            }
            else if (value == "$")
            {
                math = !math;
            }

            position = match.Index + match.Length;
        }

        Append(text[position..]);
        return spans;
    }

    /// <summary>供契约测试与诊断页：span 文本里不允许残留任何标签或脚本片段。</summary>
    public static bool ContainsRawMarkup(IEnumerable<WikiTextSpan> spans)
    {
        foreach (WikiTextSpan span in spans)
        {
            if (span.Text.Contains('<') || span.Text.Contains('>'))
            {
                return true;
            }
        }

        return false;
    }
}
