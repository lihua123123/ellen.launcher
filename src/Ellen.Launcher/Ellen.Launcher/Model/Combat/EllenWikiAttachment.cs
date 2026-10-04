// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Combat;

/// <summary>
/// ellen.wiki <c>GET /api/attachment</c> 的原始响应（「元素附着及产球」数据集）。
/// <para>
/// 该文件是唯一来源：随应用发布的快照
/// <c>Resource/CombatNotes/AvatarCombatNotes.json</c> 与运行期同步下载的内容都是这个结构，
/// 由 <see cref="AvatarCombatNoteConverter"/> 在运行时转换成角色资料页使用的
/// <see cref="AvatarCombatNote"/>。
/// </para>
/// <para>字段说明见 https://ellen.wiki/#/developer 。</para>
/// </summary>
public sealed record EllenWikiAttachmentDataset(
    [property: JsonPropertyName("elementIds")] string[]? ElementIds,
    [property: JsonPropertyName("characters")] Dictionary<string, EllenWikiAttachmentCharacter[]>? Characters);

/// <summary>
/// 数据集中的单个角色。
/// </summary>
public sealed record EllenWikiAttachmentCharacter(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("skills")] EllenWikiAttachmentSkill[]? Skills);

/// <summary>
/// 角色的一条技能 / 天赋 / 命座行。
/// </summary>
public sealed record EllenWikiAttachmentSkill(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("elementAmount")] string? ElementAmount,
    [property: JsonPropertyName("attachRule")] string? AttachRule,
    [property: JsonPropertyName("particles")] string? Particles,
    [property: JsonPropertyName("note")] string? Note,
    [property: JsonPropertyName("poise")] string? Poise);
