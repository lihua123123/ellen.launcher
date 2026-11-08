// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.UI.Xaml.Control.TextBlock.Syntax.MiHoYo;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Wiki;

public sealed class LinkMetadataContext
{
    public ImmutableDictionary<HyperLinkNameId, HyperLinkName> IdNameMap { get; init; } = default!;

    public ImmutableArray<ProudSkill> Skills { get; init; }

    public ImmutableArray<Skill> Talents { get; init; }

    public ImmutableArray<ProudSkill> Inherents { get; init; }

    public bool TryGetNameAndDescription(MiHoYoSyntaxLinkKind kind, uint id, out string name, out string description)
    {
        name = default!;
        description = default!;

        switch (kind)
        {
            case MiHoYoSyntaxLinkKind.Name:
                /* 名字表来自数据源，新内容可能引用了尚未收录的 id —— Try* 方法不该抛异常 */
                if (!IdNameMap.TryGetValue(id, out HyperLinkName? hyperLinkName))
                {
                    return false;
                }

                name = hyperLinkName.Name;
                description = hyperLinkName.Description;
                break;
            case MiHoYoSyntaxLinkKind.Inherent:
                ProudSkill? inherent = Inherents.FirstOrDefault(s => s.Id == id);
                if (inherent is null)
                {
                    return false;
                }

                name = inherent.Name;
                description = inherent.Description;
                break;
            case MiHoYoSyntaxLinkKind.Skill:
                ProudSkill? skill = Skills.FirstOrDefault(s => s.Id == id);
                if (skill is null)
                {
                    return false;
                }

                name = skill.Name;
                description = skill.Description;
                break;
            case MiHoYoSyntaxLinkKind.Talent:
                Skill? talent = Talents.FirstOrDefault(s => s.Id == id);
                if (talent is null)
                {
                    return false;
                }

                name = talent.Name;
                description = talent.Description;
                break;
            default:
                return false;
        }

        return true;
    }

    public bool TryGetParameter(MiHoYoSyntaxParameterKind kind, ReadOnlySpan<char> idSpan, out string value)
    {
        value = default!;

        if (!idSpan.TrySplitIntoTwo('|', out ReadOnlySpan<char> idSpan2, out ReadOnlySpan<char> nextSpan))
        {
            return false;
        }

        if (!uint.TryParse(idSpan2[1..], out uint id))
        {
            return false;
        }

        if (!nextSpan.TrySplitIntoTwo('S', out ReadOnlySpan<char> oneBasedIndexSpan, out ReadOnlySpan<char> factorSpan))
        {
            return false;
        }

        if (!int.TryParse(oneBasedIndexSpan, out int oneBasedIndex))
        {
            return false;
        }

        if (!int.TryParse(factorSpan, out int factor))
        {
            return false;
        }

        switch (kind)
        {
            case MiHoYoSyntaxParameterKind.ProudSkill:
                foreach (ProudSkill skill in Skills.Concat(Inherents))
                {
                    foreach ((ProudSkillId skillId, ImmutableArray<float> parameters) in skill.Proud.Parameters.IdParameters)
                    {
                        if (skillId == id)
                        {
                            value = (parameters[oneBasedIndex - 1] * factor).ToString();
                            return true;
                        }
                    }
                }

                break;
            default:
                return false;
        }

        return false;
    }
}