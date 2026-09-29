// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// 单个角色的战斗备注，数据来源于社区整理的「元素附着及产球」表。
/// <para>
/// 该数据不属于官方 metadata，因此独立存放于 <c>Resource/CombatNotes/AvatarCombatNotes.json</c>，
/// 按角色名称索引，运行时由 <see cref="Service.Combat.IAvatarCombatNoteService"/> 提供。
/// </para>
/// </summary>
public sealed record AvatarCombatNote(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("entries")] CombatNoteEntry[] Entries);
