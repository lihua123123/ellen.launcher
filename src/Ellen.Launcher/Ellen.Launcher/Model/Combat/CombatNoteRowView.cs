// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 备注表格中一行的渲染模型。
/// </summary>
public sealed class CombatNoteRowView
{
    public CombatNoteRowView(CombatNoteRow row)
    {
        Skill = row.Skill;
        ElementAmount = row.ElementAmount;
        Attachment = row.Attachment;
        Particle = row.Particle;
        Remark = row.Remark;
        Poise = row.Poise;
    }

    public string Skill { get; }

    public string? ElementAmount { get; }

    public string? Attachment { get; }

    public string? Particle { get; }

    public string? Remark { get; }

    public string? Poise { get; }
}
