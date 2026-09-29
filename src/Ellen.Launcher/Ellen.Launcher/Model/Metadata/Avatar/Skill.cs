// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Combat;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public class Skill
{
    public required SkillId Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public string? SpecialDescription { get; init; }

    public required string Icon { get; init; }

    public ExtraLevelInfo? ExtraLevel { get; init; }

    /// <summary>
    /// 「元素附着及产球」备注表，无收录时为 <see langword="null"/>。
    /// </summary>
    public CombatNoteTable? CombatNoteTable { get; set; }
}