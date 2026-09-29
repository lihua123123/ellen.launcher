// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 战斗备注中的一行，对应「元素附着及产球」表里的某次攻击 / 某个技能 / 某个命座。
/// 除 <see cref="Skill"/> 外均为可空，缺省表示表中该单元格为空。
/// </summary>
public sealed record CombatNoteRow(
    [property: JsonPropertyName("skill")] string Skill,
    [property: JsonPropertyName("elementAmount")] string? ElementAmount,
    [property: JsonPropertyName("attachment")] string? Attachment,
    [property: JsonPropertyName("particle")] string? Particle,
    [property: JsonPropertyName("poise")] string? Poise,
    [property: JsonPropertyName("remark")] string? Remark);
