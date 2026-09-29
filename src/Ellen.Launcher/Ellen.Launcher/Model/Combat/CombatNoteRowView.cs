// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 备注表格中一行的渲染模型。
/// <see cref="ShowSkill"/> 为 <see langword="false"/> 时，该行不占用「技能」列（用于只有一行的表格）。
/// </summary>
public sealed class CombatNoteRowView
{
    public CombatNoteRowView(bool showSkill, CombatNoteRow row)
    {
        ShowSkill = showSkill;
        Skill = row.Skill;
        ElementAmount = row.ElementAmount;
        Attachment = row.Attachment;
        Particle = row.Particle;
        Poise = row.Poise;
        Remark = row.Remark;
    }

    public bool ShowSkill { get; }

    public string Skill { get; }

    public string? ElementAmount { get; }

    public string? Attachment { get; }

    public string? Particle { get; }

    public string? Poise { get; }

    public string? Remark { get; }
}
