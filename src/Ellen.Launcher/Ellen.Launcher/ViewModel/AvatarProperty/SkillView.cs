// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Calculable;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public sealed class SkillView : NameIconDescription, ITypedCalculableSource<ICalculableSkill, SkillType>
{
    public LevelParameters<string, ParameterDescription> Info { get; set; } = default!;

    public uint LevelNumber { get; set; }

    public string Level { get; set; } = default!;

    public SkillGroupId GroupId { get; set; }

    public string? SpecialDescription { get; set; }

    public ICalculableSkill ToCalculable(SkillType type)
    {
        return CalculableSkill.From(this, type);
    }
}
