// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 战斗备注中某个条目（技能 / 固有天赋 / 命座）对应的行。
/// <see cref="Target"/> 为 <see cref="CombatNoteTarget"/> 的名称，
/// 在运行时由 <c>AvatarCombatNoteService</c> 解析，避免依赖枚举的 JSON 转换器。
/// </summary>
public sealed record CombatNoteEntry(
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("rows")] CombatNoteRow[] Rows);
