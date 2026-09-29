// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Calculable;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class ProudSkill : Skill, ITypedCalculableSource<ICalculableSkill, SkillType>
{
    public required SkillGroupId GroupId { get; init; }

    public required DescriptionsParameters Proud { get; init; }

    public required EnergyType SpecialEnergyType { get; init; }

    public float? CdTime { get; set; }

    public static uint GetMaxLevel()
    {
        return 10U;
    }

    public ICalculableSkill ToCalculable(SkillType type)
    {
        return CalculableSkill.From(this, type);
    }
}