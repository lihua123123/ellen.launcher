// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Media;
using System.Collections.Frozen;
using Windows.UI;

namespace Ellen.Launcher.UI.Xaml.Control;

/// <summary>
/// 元素 / 反应配色与中文关键词表（可复用组件）。
/// <para>
/// 配色内置自 ellen-wiki 的 <c>content/meta/colors.json</c>：渐变项取其中间色
/// （WinUI 的 <see cref="Microsoft.UI.Xaml.Documents.Run"/> 不做 background-clip:text 文字渐变）。
/// </para>
/// <para>
/// 中文关键词取自 ellen-wiki 内容里实际使用的写法（<c>月感电</c> / <c>月绽放</c> / <c>月结晶</c> /
/// <c>星超导</c> / <c>…属性星扩散</c>），配合单字元素名，供
/// <see cref="GuideRichText"/> 的自动着色与后续功能复用。
/// </para>
/// </summary>
public static class ElementPalette
{
    /// <summary>配色键 → 颜色，值为 <c>#RRGGBB</c>。</summary>
    public static readonly FrozenDictionary<string, string> Colors = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["cryo"] = "#22D3EE",
        ["electro"] = "#7950F2",
        ["anemo"] = "#34D399",
        ["pyro"] = "#EE0000",
        ["geo"] = "#EAB308",
        ["hydro"] = "#0066CC",
        ["dendro"] = "#4ADE80",
        ["lunar-charged-electro"] = "#C7B0F0",
        ["lunar-bloom-dendro"] = "#B0FFB0",
        ["lunar-crystallize-geo"] = "#FCEAB8",
        ["stellar-conduct-electro"] = "#9AAEF0",
        ["stellar-conduct-cryo"] = "#8EEBE7",
        ["stellar-swirl-anemo"] = "#4FE0B0",
        ["stellar-swirl-cryo"] = "#7BA5C6",

        // colors.json 的 genshin 段没有物理，这里补一个中性灰（仅用于「物理」字样着色）。
        ["physical"] = "#B8B8B8",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, Brush> BrushCache = Colors
        .ToFrozenDictionary(pair => pair.Key, pair => (Brush)CreateBrush(pair.Value), StringComparer.Ordinal);

    /// <summary>
    /// 多字关键词（长词优先，避免「星超导」被拆成「星」+「超导」）。
    /// </summary>
    private static readonly (string Keyword, string Key)[] MultiCharKeywords =
    [
        ("风属性星扩散", "stellar-swirl-anemo"),
        ("冰属性星扩散", "stellar-swirl-cryo"),
        ("月感电", "lunar-charged-electro"),
        ("月绽放", "lunar-bloom-dendro"),
        ("月结晶", "lunar-crystallize-geo"),
        ("星超导", "stellar-conduct-cryo"),
        ("物理", "physical"),
    ];

    private static readonly FrozenDictionary<char, string> SingleCharKeywords = new Dictionary<char, string>
    {
        ['火'] = "pyro",
        ['水'] = "hydro",
        ['草'] = "dendro",
        ['雷'] = "electro",
        ['风'] = "anemo",
        ['冰'] = "cryo",
        ['岩'] = "geo",
    }.ToFrozenDictionary();

    /// <summary>取配色画刷（常驻缓存，可安全重复取用）。</summary>
    public static bool TryGetBrush(string key, [NotNullWhen(true)] out Brush? brush)
    {
        return BrushCache.TryGetValue(key, out brush);
    }

    /// <summary>
    /// 尝试在 <paramref name="text"/> 的 <paramref name="index"/> 处匹配关键词。
    /// </summary>
    public static bool TryMatchKeyword(string text, int index, out string key, out int length)
    {
        foreach ((string keyword, string mapped) in MultiCharKeywords)
        {
            if (index + keyword.Length <= text.Length
                && string.CompareOrdinal(text, index, keyword, 0, keyword.Length) == 0)
            {
                key = mapped;
                length = keyword.Length;
                return true;
            }
        }

        if (SingleCharKeywords.TryGetValue(text[index], out string? single))
        {
            key = single;
            length = 1;
            return true;
        }

        key = string.Empty;
        length = 0;
        return false;
    }

    private static Color CreateColor(string hex)
    {
        string value = hex.TrimStart('#');
        return new Color
        {
            A = 0xFF,
            R = Convert.ToByte(value[..2], 16),
            G = Convert.ToByte(value.Substring(2, 2), 16),
            B = Convert.ToByte(value.Substring(4, 2), 16),
        };
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        return new SolidColorBrush(CreateColor(hex));
    }
}
