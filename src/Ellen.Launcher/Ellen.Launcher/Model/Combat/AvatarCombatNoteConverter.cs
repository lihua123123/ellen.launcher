// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Text.RegularExpressions;

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 把 ellen.wiki 的「元素附着及产球」原始数据（<see cref="EllenWikiAttachmentDataset"/>）
/// 转换成角色资料页使用的 <see cref="AvatarCombatNote"/>。
/// <para>
/// 随包快照与运行期同步下载的数据共用这一份转换规则，因此这里是指定技能归属、
/// 归一化附着规则、补全技能列名的唯一实现。
/// </para>
/// </summary>
public static partial class AvatarCombatNoteConverter
{
    /// <summary>
    /// 转换整个数据集。输出顺序与数据集的 <c>elementIds</c> / 角色顺序一致。
    /// </summary>
    public static AvatarCombatNote[] Convert(EllenWikiAttachmentDataset dataset)
    {
        if (dataset.Characters is not { } characters)
        {
            return [];
        }

        string[] elementIds = dataset.ElementIds ?? [.. characters.Keys];

        List<AvatarCombatNote> notes = [];
        foreach (string element in elementIds)
        {
            if (!characters.TryGetValue(element, out EllenWikiAttachmentCharacter[]? elementCharacters))
            {
                continue;
            }

            foreach (EllenWikiAttachmentCharacter character in elementCharacters)
            {
                if (string.IsNullOrEmpty(character.Name))
                {
                    continue;
                }

                if (ConvertCharacter(character) is { } note)
                {
                    notes.Add(note);
                }
            }
        }

        return [.. notes];
    }

    private static AvatarCombatNote? ConvertCharacter(EllenWikiAttachmentCharacter character)
    {
        List<CombatNoteTarget> order = [];
        Dictionary<CombatNoteTarget, List<SkillRow>> groups = [];

        foreach (EllenWikiAttachmentSkill skill in character.Skills ?? [])
        {
            if (string.IsNullOrEmpty(skill.Name))
            {
                continue;
            }

            SkillRow row = new(
                skill.Name,
                skill.ElementAmount ?? string.Empty,
                ConvertAttachment(skill.AttachRule ?? string.Empty),
                skill.Particles ?? string.Empty,
                skill.Poise ?? string.Empty,
                skill.Note ?? string.Empty);

            // 除技能名外整行全空 —— 视为占位行丢弃。
            if (row is { ElementAmount: "", Attachment: "", Particle: "", Poise: "", Remark: "" })
            {
                continue;
            }

            foreach (CombatNoteTarget target in Classify(skill.Name))
            {
                if (!groups.TryGetValue(target, out List<SkillRow>? rows))
                {
                    rows = [];
                    groups[target] = rows;
                    order.Add(target);
                }

                rows.Add(row);
            }
        }

        if (order.Count is 0)
        {
            return null;
        }

        List<CombatNoteEntry> entries = [];
        foreach (CombatNoteTarget target in order)
        {
            List<SkillRow> rows = groups[target];
            bool multi = rows.Count > 1;

            entries.Add(new CombatNoteEntry(
                target.ToString(),
                [.. rows.Select(row => new CombatNoteRow(
                    ConvertSkillLabel(row.Skill, target, multi),
                    row.ElementAmount,
                    row.Attachment,
                    row.Particle,
                    row.Poise,
                    row.Remark))]));
        }

        return new AvatarCombatNote(character.Name, [.. entries]);
    }

    /// <summary>
    /// 把一个技能名映射到角色资料页里的天赋卡片。
    /// 归属规则：命座 N → ConstellationN，突破/固有天赋 N → InherentN，
    /// e/E 开头 → ElementalSkill，Q 开头 → ElementalBurst，其余 → NormalAttack。
    /// </summary>
    private static List<CombatNoteTarget> Classify(string skill)
    {
        string s = skill.Trim();

        if (ConstellationPrefixRegex().Match(s) is { Success: true } constellation)
        {
            return ParseNumbered(constellation.Groups[1].Value, "Constellation");
        }

        if (InherentPrefixRegex().Match(s) is { Success: true } inherent)
        {
            return ParseNumbered(inherent.Groups[1].Value, "Inherent");
        }

        if (s.Length > 0 && s[0] is 'e' or 'E')
        {
            return [CombatNoteTarget.ElementalSkill];
        }

        if (s.Length > 0 && s[0] is 'Q')
        {
            return [CombatNoteTarget.ElementalBurst];
        }

        return [CombatNoteTarget.NormalAttack];
    }

    private static List<CombatNoteTarget> ParseNumbered(string value, string prefix)
    {
        List<CombatNoteTarget> targets = [];
        foreach (Match match in NumberRegex().Matches(value))
        {
            if (Enum.TryParse($"{prefix}{match.Value}", ignoreCase: true, out CombatNoteTarget target))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    /// <summary>
    /// 归一化附着规则："3 hits / 1 s" 或 "2.5 s / 3 hit" → "3 hit/1 s" 形式。
    /// </summary>
    private static string ConvertAttachment(string value)
    {
        string s = value.Trim();

        if (HitsPerSecondRegex().Match(s) is { Success: true } hits)
        {
            return $"{hits.Groups[1].Value} hit/{hits.Groups[2].Value} s";
        }

        if (SecondsPerHitsRegex().Match(s) is { Success: true } seconds)
        {
            return $"{seconds.Groups[2].Value} hit/{seconds.Groups[1].Value} s";
        }

        return s;
    }

    /// <summary>
    /// 同一组里有多行时才在「技能」列写全称（"元素战技 变招一"），只有一行时留空。
    /// </summary>
    private static string ConvertSkillLabel(string skill, CombatNoteTarget target, bool multi)
    {
        if (!multi)
        {
            return string.Empty;
        }

        string s = skill.Trim();
        return target switch
        {
            CombatNoteTarget.ElementalSkill => SkillPrefixRegex().Replace(s, "元素战技 ").Trim(),
            CombatNoteTarget.ElementalBurst => BurstPrefixRegex().Replace(s, "元素爆发 ").Trim(),
            _ => s,
        };
    }

    [GeneratedRegex(@"^([\d.]+)\s*hits?\s*/\s*([\d.]+)\s*s$")]
    private static partial Regex HitsPerSecondRegex();

    [GeneratedRegex(@"^([\d.]+)\s*s\s*/\s*([\d.]+)\s*hits?$")]
    private static partial Regex SecondsPerHitsRegex();

    [GeneratedRegex(@"^命座\s*([\d/、,]+)")]
    private static partial Regex ConstellationPrefixRegex();

    [GeneratedRegex(@"^(?:突破|固有)天赋\s*([\d/、,]+)")]
    private static partial Regex InherentPrefixRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"^[eE](/[eE])?\s*")]
    private static partial Regex SkillPrefixRegex();

    [GeneratedRegex(@"^Q\s*")]
    private static partial Regex BurstPrefixRegex();

    private readonly record struct SkillRow(
        string Skill,
        string ElementAmount,
        string Attachment,
        string Particle,
        string Poise,
        string Remark);
}
