// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Text.RegularExpressions;

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 把 ellen.wiki 的「元素附着及产球」原始数据（<see cref="EllenWikiAttachmentDataset"/>）
/// 转换成角色资料页使用的 <see cref="AvatarCombatNote"/>。
/// <para>
/// 随包快照与运行期同步下载的数据共用这一份转换规则。技能名与元素量 / 附着规则 / 产球 /
/// 备注 / 抗打断一律原样透传，与网站展示保持一致；这里只负责把技能名归到对应的天赋卡片。
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
                skill.AttachRule ?? string.Empty,
                skill.Particles ?? string.Empty,
                skill.Note ?? string.Empty,
                skill.Poise ?? string.Empty);

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
            entries.Add(new CombatNoteEntry(
                target.ToString(),
                [.. groups[target].Select(static row => new CombatNoteRow(
                    row.Skill,
                    row.ElementAmount,
                    row.Attachment,
                    row.Particle,
                    row.Remark,
                    row.Poise))]));
        }

        return new AvatarCombatNote(character.Name, [.. entries]);
    }

    /// <summary>
    /// 把一个技能名映射到角色资料页里的天赋卡片。
    /// 归属规则：命座 N → ConstellationN，突破/固有天赋 N → InherentN，
    /// e/E 开头 → ElementalSkill，Q 开头 → ElementalBurst，含「冲刺」→ Sprint，
    /// 其余 → NormalAttack。
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

        // 替代冲刺（网站也单独归为 sprint），如莫娜「冲刺」、神里绫华「冲刺」。
        if (s.Contains("冲刺", StringComparison.Ordinal))
        {
            return [CombatNoteTarget.Sprint];
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

    [GeneratedRegex(@"^命座\s*([\d/、,]+)")]
    private static partial Regex ConstellationPrefixRegex();

    [GeneratedRegex(@"^(?:突破|固有)天赋\s*([\d/、,]+)")]
    private static partial Regex InherentPrefixRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberRegex();

    private readonly record struct SkillRow(
        string Skill,
        string ElementAmount,
        string Attachment,
        string Particle,
        string Remark,
        string Poise);
}
