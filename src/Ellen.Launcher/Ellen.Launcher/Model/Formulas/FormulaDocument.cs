// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Formulas;

/// <summary>
/// 「计算公式」页面的数据，来源于随应用发布的
/// <c>Resource/Formulas/GenshinFormulas.json</c>（由 <c>tools/FormulasGen/gen.mjs</c> 从
/// ellen-wiki 的 <c>content/formulas/genshin.md</c> 生成）。
/// </summary>
public sealed class FormulaDocument
{
    [JsonPropertyName("game")]
    public string Game { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public ImmutableArray<FormulaSection> Sections { get; set; } = [];
}

/// <summary>
/// 一个 <c>##</c> 章节。
/// </summary>
public sealed class FormulaSection
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("blocks")]
    public ImmutableArray<FormulaBlock> Blocks { get; set; } = [];
}

/// <summary>
/// 章节内的一个内容块。<see cref="Kind"/> 决定实际语义：
/// <c>p</c>（段落）/ <c>h</c>（子标题）/ <c>formula</c>（块公式）/
/// <c>table</c>（表格）/ <c>quote</c>（引用）。
/// </summary>
public sealed class FormulaBlock
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// <c>p</c> / <c>h</c> 的正文，保留 ellen-wiki 的行内标记
    /// （<c>**加粗**</c>、<c>`专有名词`</c>、<c>&lt;span class="pyro"&gt;</c>），
    /// 行内公式已在生成期转成普通文本。
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>
    /// <c>h</c> 的层级（3 表示 <c>###</c>）。
    /// </summary>
    [JsonPropertyName("level")]
    public int Level { get; set; }

    /// <summary>
    /// <c>formula</c> 的图片标识（不含扩展名与目录）。
    /// </summary>
    [JsonPropertyName("image")]
    public string? Image { get; set; }

    /// <summary>
    /// <c>formula</c> 的自然宽度（DIP）。
    /// </summary>
    [JsonPropertyName("width")]
    public double Width { get; set; }

    /// <summary>
    /// <c>formula</c> 的自然高度（DIP）。
    /// </summary>
    [JsonPropertyName("height")]
    public double Height { get; set; }

    /// <summary>
    /// 公式的 LaTeX 原文，仅用于诊断。
    /// </summary>
    [JsonPropertyName("tex")]
    public string? Tex { get; set; }

    /// <summary>
    /// <c>table</c> 的表头。
    /// </summary>
    [JsonPropertyName("head")]
    public ImmutableArray<string> Head { get; set; } = [];

    /// <summary>
    /// <c>table</c> 的数据行。
    /// </summary>
    [JsonPropertyName("rows")]
    public ImmutableArray<ImmutableArray<string>> Rows { get; set; } = [];

    /// <summary>
    /// <c>quote</c> 的内部块。
    /// </summary>
    [JsonPropertyName("blocks")]
    public ImmutableArray<FormulaBlock> Blocks { get; set; } = [];

    public bool IsParagraph => Kind is "p";

    public bool IsHeading => Kind is "h";

    public bool IsFormula => Kind is "formula";

    public bool IsTable => Kind is "table";

    public bool IsQuote => Kind is "quote";
}
