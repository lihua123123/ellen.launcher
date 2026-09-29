// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Combat;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;

namespace Ellen.Launcher.Service.Combat;

/// <summary>
/// 提供角色战斗备注（元素附着 / 产球 / 抗打断）的只读查询。
/// </summary>
public interface IAvatarCombatNoteService
{
    /// <summary>
    /// 按角色名称获取战斗备注，未收录时返回 <see langword="null"/>。
    /// </summary>
    AvatarCombatNote? GetForAvatar(string avatarName);

    /// <summary>
    /// 把备注中的每一组行挂到对应技能 / 固有天赋 / 命座的 <c>CombatNoteTable</c> 上。
    /// </summary>
    void ApplyTo(MetadataAvatar avatar);
}
