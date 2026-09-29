// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;
using System.IO;
using System.Text;

namespace Ellen.Launcher.Model.Endgame;

/// <summary>
/// 幽境危战（幽境 Boss）攻略数据，来源于随应用发布的
/// <c>Resource/Endgame/HardChallengeBosses.json</c>。
/// </summary>
public sealed class HardChallengeBossGuidePeriod
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("bosses")]
    public ImmutableArray<HardChallengeBossGuideBoss> Bosses { get; set; } = [];
}

/// <summary>
/// 单个幽境 Boss。
/// </summary>
public sealed class HardChallengeBossGuideBoss
{
    [JsonPropertyName("shortName")]
    public string ShortName { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("hp")]
    public string? Hp { get; set; }

    /// <summary>
    /// 难度 5 的血量（已从 <see cref="Hp"/> 中拆出）。
    /// </summary>
    public string? HpN5 { get; private set; }

    /// <summary>
    /// 难度 6 的血量（已从 <see cref="Hp"/> 中拆出）。
    /// </summary>
    public string? HpN6 { get; private set; }

    public bool HasHp => !string.IsNullOrEmpty(HpN5) || !string.IsNullOrEmpty(HpN6);

    [JsonPropertyName("imgLocal")]
    public string? ImageFileName { get; set; }

    [JsonPropertyName("imgMissing")]
    public bool IsImageMissing { get; set; }

    [JsonPropertyName("skills")]
    public ImmutableArray<string> Skills { get; set; } = [];

    /// <summary>
    /// 本地图片 URI（file:// 形式，非打包模式下最可靠），图片缺失或未收录时为 <see langword="null"/>。
    /// </summary>
    public Uri? ImageUri { get; private set; }

    /// <summary>
    /// 机制 / 介绍 / 背景 三段正文。
    /// </summary>
    public ImmutableArray<HardChallengeGuideSection> Sections { get; private set; } = [];

    /// <summary>
    /// 「机制」段（战斗要点），直接展示在 Boss 卡片里。
    /// </summary>
    public HardChallengeGuideSection? Mechanism => Sections is [var first, ..] ? first : null;

    public bool HasMechanism => Mechanism is { Lines.Length: > 0 };

    /// <summary>
    /// 「介绍」与「背景」段，展示在卡片的详情浮出层里。
    /// </summary>
    public ImmutableArray<HardChallengeGuideSection> DetailSections { get; private set; } = [];

    public bool HasDetail => !DetailSections.IsDefaultOrEmpty;

    public bool HasImage { get; private set; }

    /// <summary>
    /// 把本地绝对路径转换为 <c>file:///</c> 形式的绝对 URI。
    /// <para>
    /// 注意：<c>new Uri(@"D:\a\b.png")</c> 会抛出 <see cref="UriFormatException"/>，
    /// 因为单个字母会被当作 scheme，必须先拼成 file URI 再构造。
    /// </para>
    /// </summary>
    private static Uri ToFileUri(string absolutePath)
    {
        return new Uri($"file:///{absolutePath.Replace('\\', '/')}");
    }

    /// <summary>
    /// 上游写法固定为 <c>N5 / N6：765,8830 / 1726,0004</c>（逗号是万分位分隔符），
    /// 这里只做拆分，不做数值换算。
    /// </summary>
    private void ParseHp()
    {
        if (string.IsNullOrWhiteSpace(Hp))
        {
            return;
        }

        string value = Hp;
        int separatorIndex = value.IndexOfAny(['：', ':']);
        if (separatorIndex >= 0)
        {
            value = value[(separatorIndex + 1)..];
        }

        string[] parts = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length > 0)
        {
            HpN5 = parts[0];
        }

        if (parts.Length > 1)
        {
            HpN6 = parts[1];
        }
    }

    /// <summary>
    /// 解析上游使用 <c>__SEP__</c> 分隔的分段正文。
    /// </summary>
    /// <param name="sectionTitles">三段正文的本地化标题。</param>
    /// <param name="imageFolder">配图所在目录。</param>
    public void Parse(ImmutableArray<string> sectionTitles, string imageFolder)
    {
        string? imagePath = string.IsNullOrEmpty(ImageFileName) ? null : Path.Combine(imageFolder, ImageFileName!);
        HasImage = !IsImageMissing && imagePath is not null && File.Exists(imagePath);
        ImageUri = HasImage ? ToFileUri(imagePath!) : null;

        ParseHp();

        if (Skills.IsDefaultOrEmpty)
        {
            return;
        }

        List<HardChallengeGuideSection> sections = [];
        List<HardChallengeGuideLine> lines = [];
        int index = 0;

        foreach (string raw in Skills)
        {
            if (raw is "__SEP__")
            {
                Flush();
                continue;
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            lines.Add(HardChallengeGuideLine.Create(raw));
        }

        Flush();
        Sections = [.. sections];
        DetailSections = [.. sections.Skip(1)];

        void Flush()
        {
            if (lines.Count is 0)
            {
                return;
            }

            string title = index < sectionTitles.Length ? sectionTitles[index] : string.Empty;
            sections.Add(new(title, [.. lines]));
            lines.Clear();
            index++;
        }
    }
}

/// <summary>
/// 幽境 Boss 攻略中的一段（机制 / 介绍 / 背景）。
/// </summary>
public sealed class HardChallengeGuideSection
{
    public HardChallengeGuideSection(string title, ImmutableArray<HardChallengeGuideLine> lines)
    {
        Title = title;
        Lines = lines;
    }

    public string Title { get; }

    public ImmutableArray<HardChallengeGuideLine> Lines { get; }

    public bool HasTitle => !string.IsNullOrEmpty(Title);
}

/// <summary>
/// 攻略中的一行。<see cref="IsHeading"/> 为真时表示上游写成了 <c>**标题**</c>。
/// </summary>
public sealed class HardChallengeGuideLine
{
    private HardChallengeGuideLine(string text, string raw, bool isHeading)
    {
        Text = text;
        Raw = raw;
        IsHeading = isHeading;
    }

    /// <summary>
    /// 去掉富文本标记后的纯文本（用于标题行、可选中复制）。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 原始行，保留上游的 <c>**加粗**</c>、<c>`专有名词`</c>、<c>$公式$</c> 等标记，
    /// 交给 <c>GuideRichTextBlock</c> 渲染成富文本。
    /// </summary>
    public string Raw { get; }

    public bool IsHeading { get; }

    public static HardChallengeGuideLine Create(string raw)
    {
        string text = raw.Trim();
        bool isHeading = text.StartsWith("**", StringComparison.Ordinal) && text.EndsWith("**", StringComparison.Ordinal) && text.Length > 4;
        return new(CleanText(text), text, isHeading);
    }

    /// <summary>
    /// 把上游的富文本标记收敛成纯文本：去掉元素着色外壳、反引号与行内公式的分隔符，
    /// 公式内容（如 <c>$30s$</c>）本身保留。
    /// </summary>
    private static string CleanText(string value)
    {
        StringBuilder builder = new(value.Length);
        bool insideTag = false;

        foreach (char c in value)
        {
            switch (c)
            {
                case '<':
                    insideTag = true;
                    break;
                case '>':
                    insideTag = false;
                    break;
                case '*':
                case '`':
                case '$':
                    break;
                case '\\':
                    break;
                default:
                    if (!insideTag)
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        // Collapse the spaces left behind by the removed markup.
        string result = builder.ToString();
        while (result.Contains("  ", StringComparison.Ordinal))
        {
            result = result.Replace("  ", " ", StringComparison.Ordinal);
        }

        return result.Trim();
    }
}
