// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class SkillDepot
{
    public required Arkhe Arkhe { get; init; }

    public required ImmutableArray<ProudSkill> Skills { get; init; }

    public required ProudSkill EnergySkill { get; init; }
    public ImmutableArray<ProudSkill>? SpecialSkills { get; init; }

    public required ImmutableArray<ProudSkill> Inherents { get; init; }

    public required ImmutableArray<Skill> Talents { get; init; }

    public ImmutableArray<ProudSkill> CompositeSkills { get => !field.IsDefault ? field : field = [.. Skills, EnergySkill, .. SpecialSkills ?? ImmutableArray<ProudSkill>.Empty, .. Inherents]; }

    // No Inherents && 跳过 替换冲刺的技能
    public ImmutableArray<ProudSkill> CompositeSkillsNoInherents { get => !field.IsDefault ? field : field = [.. Skills.Where(s => s.Proud.Parameters.Count > 1), EnergySkill]; }
}