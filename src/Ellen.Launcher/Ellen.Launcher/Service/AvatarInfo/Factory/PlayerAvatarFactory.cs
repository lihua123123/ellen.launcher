// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;
using IdLevelParametersOfSkill = Ellen.Launcher.Model.Metadata.IdLevelParameters<Ellen.Launcher.Model.Primitive.ProudSkillId, Ellen.Launcher.Model.Primitive.SkillLevel, float>;
using WebSkill = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar.Skill;
using WebSkillType = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar.SkillType;
using WebConstellation = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar.Constellation;
using WebDetailedCharacter = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar.DetailedCharacter;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

/// <summary>
/// 旅行者（空/荧）在随包元数据中没有可用的资料：既没有 <see cref="SkillDepot"/>，也没有元素与名片。
/// 这里以战绩接口返回的实时数据（元素、技能、命之座）补齐一份可直接用于面板渲染的元数据。
/// </summary>
public static partial class PlayerAvatarFactory
{
    private const string NormalAttackIconName = "Skill_A_01";
    private const string NameCardPicturePrefix = "UI_NameCardPic_0";
    private const uint SkillParameterMaxLevel = 15;

    public static MetadataAvatar Create(MetadataAvatar baseAvatar, WebDetailedCharacter character)
    {
        ElementType element = (ElementType)(int)character.Base.Element;
        string elementName = element.GetLocalizedDescriptionOrDefault(SH.ResourceManager, CultureInfo.CurrentCulture) ?? string.Empty;

        return new()
        {
            Id = baseAvatar.Id,
            PromoteId = baseAvatar.PromoteId,
            Sort = baseAvatar.Sort,
            Body = baseAvatar.Body,
            Icon = baseAvatar.Icon,
            SideIcon = baseAvatar.SideIcon,
            Name = SH.ModelMetadataAvatarPlayerName,
            Description = baseAvatar.Description,
            BeginTime = baseAvatar.BeginTime,
            Quality = baseAvatar.Quality,
            IsPlaceholder = true,

            // 旅行者固定使用单手剑
            Weapon = WeaponType.WEAPON_SWORD_ONE_HAND,
            BaseValue = baseAvatar.BaseValue,
            GrowCurves = baseAvatar.GrowCurves,
            SkillDepot = CreateSkillDepot(element, character),
            FetterInfo = new()
            {
                Title = string.Empty,
                Detail = string.Empty,
                Association = default,
                Native = string.Empty,
                BirthMonth = default,
                BirthDay = default,
                VisionBefore = elementName,
                ConstellationBefore = string.Empty,
                CvChinese = string.Empty,
                CvJapanese = string.Empty,
                CvEnglish = string.Empty,
                CvKorean = string.Empty,
                Fetters = [],
                FetterStories = [],
            },
            Costumes = [],
            CultivationItems = [],
            NameCard = new()
            {
                Name = string.Empty,
                Description = string.Empty,
                Icon = string.Empty,
                PicturePrefix = NameCardPicturePrefix,
            },
        };
    }

    private static SkillDepot CreateSkillDepot(ElementType element, WebDetailedCharacter character)
    {
        string suffix = GetElementIconSuffix(element);

        List<WebSkill> fightSkills = [];
        List<WebSkill> inherentSkills = [];

        // 只有 skill_type=Fight 参与「普攻/元素战技/元素爆发」的排位。
        // 米游社还会返回 skill_type=3 的特殊技能（如旅行者的星烁相关技能），
        // 若按「非固有即战斗」分组，它会挤掉元素爆发的位置。
        foreach (WebSkill skill in character.Skills)
        {
            (skill.SkillType is WebSkillType.Fight ? fightSkills : inherentSkills).Add(skill);
        }

        Dictionary<SkillId, string> skillNames = ExtractSkillNames(character.Constellations);

        // 战斗天赋的顺序固定为：普通攻击、元素战技、元素爆发
        List<ProudSkill> fightProudSkills = new(fightSkills.Count);
        for (int index = 0; index < fightSkills.Count; index++)
        {
            SkillId skillId = fightSkills[index].SkillId;

            (string Name, string Icon) descriptor = index switch
            {
                0 => (SH.ModelMetadataAvatarPlayerSkillNormalAttack, NormalAttackIconName),
                1 => (SH.ModelMetadataAvatarPlayerSkillElementalSkill, $"Skill_S_Player{suffix}_01"),
                _ => (SH.ModelMetadataAvatarPlayerSkillElementalBurst, $"Skill_E_Player{suffix}_01"),
            };

            fightProudSkills.Add(CreateProudSkill(skillId, skillNames.GetValueOrDefault(skillId, descriptor.Name), descriptor.Icon));
        }

        ImmutableArray<ProudSkill>.Builder inherentProudSkills = ImmutableArray.CreateBuilder<ProudSkill>(inherentSkills.Count);
        for (int index = 0; index < inherentSkills.Count; index++)
        {
            SkillId skillId = inherentSkills[index].SkillId;
            string name = skillNames.GetValueOrDefault(skillId, SH.ModelMetadataAvatarPlayerSkillInherent);
            inherentProudSkills.Add(CreateProudSkill(skillId, name, $"UI_Talent_S_Player{suffix}_{(index + 2):D2}"));
        }

        return new()
        {
            Arkhe = Arkhe.None,
            Skills = fightProudSkills.Count > 1 ? [.. fightProudSkills.Take(fightProudSkills.Count - 1)] : [],
            EnergySkill = fightProudSkills.Count > 0 ? fightProudSkills[^1] : CreateProudSkill(default, string.Empty, string.Empty),
            Inherents = inherentProudSkills.ToImmutable(),
            Talents = [.. character.Constellations.Select((constellation, index) => new Skill
            {
                Id = constellation.Id,
                Name = constellation.Name,
                Description = constellation.Effect,
                Icon = GetConstellationIcon(constellation, suffix, index),
            })],
        };
    }

    [GeneratedRegex(@"\{LINK#S(\d+)\}(.+?)\{/LINK\}")]
    private static partial Regex SkillLinkRegex { get; }

    /// <summary>
    /// 米游社命座的效果文本里会用 <c>{LINK#S&lt;技能Id&gt;}&lt;技能名&gt;{/LINK}</c> 引用技能，
    /// 借此拿到旅行者真实的技能名（本地元数据没有旅行者资料，只能这样取）。
    /// </summary>
    private static Dictionary<SkillId, string> ExtractSkillNames(ImmutableArray<WebConstellation> constellations)
    {
        Dictionary<SkillId, string> names = [];

        foreach (WebConstellation constellation in constellations)
        {
            if (string.IsNullOrEmpty(constellation.Effect))
            {
                continue;
            }

            foreach (Match match in SkillLinkRegex.Matches(constellation.Effect))
            {
                if (uint.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint id))
                {
                    names[(SkillId)id] = match.Groups[2].Value;
                }
            }
        }

        return names;
    }

    // 米游社的命座图标通常直接给出完整地址（act-webstatic.mihoyo.com），
    // 原样交给 IconNameToUri；只有在它没给图标时才按元素规则兜底。
    private static string GetConstellationIcon(WebConstellation constellation, string suffix, int index)
    {
        if (string.IsNullOrEmpty(constellation.Icon))
        {
            return $"UI_Talent_S_Player{suffix}_{(index + 1):D2}";
        }

        return Uri.TryCreate(constellation.Icon, UriKind.Absolute, out _)
            ? constellation.Icon
            : NormalizeIconName(constellation.Icon);
    }

    private static ProudSkill CreateProudSkill(SkillId skillId, string name, string icon)
    {
        return new()
        {
            Id = skillId,
            GroupId = default,
            Name = name,

            // 本地没有旅行者的技能数值表，这里至少让详情浮层显示技能名，
            // 否则点开是一个完全空白的浮层，看起来像没响应。
            Description = name.Length > 0 ? SH.FormatModelMetadataAvatarPlayerSkillNoData(name) : string.Empty,
            Icon = icon,
            SpecialEnergyType = EnergyType.SPECIAL_ENERGY_NONE,
            Proud = CreateEmptyParameters(),
        };
    }

    // 旅行者的技能数值仅由战绩接口实时返回，本地没有参数表，
    // 这里仍需要为每个等级占位，避免命中等级时抛出 KeyNotFoundException。
    private static DescriptionsParameters CreateEmptyParameters()
    {
        ImmutableArray<IdLevelParametersOfSkill>.Builder builder =
            ImmutableArray.CreateBuilder<IdLevelParametersOfSkill>((int)SkillParameterMaxLevel);

        for (uint level = 1; level <= SkillParameterMaxLevel; level++)
        {
            builder.Add(new((ProudSkillId)level, (SkillLevel)level, []));
        }

        return new()
        {
            Descriptions = [],
            Parameters = new(builder.ToImmutable()),
        };
    }

    // 旅行者天赋图标以元素为后缀：UI_Talent_S_Player{Element}_NN
    private static string GetElementIconSuffix(ElementType element)
    {
        return element switch
        {
            ElementType.Fire => "Fire",
            ElementType.Water => "Water",
            ElementType.Grass => "Grass",
            ElementType.Electric => "Electric",
            ElementType.Ice => "Ice",
            ElementType.Wind => "Wind",
            ElementType.Rock => "Rock",
            _ => string.Empty,
        };
    }

    private static string NormalizeIconName(string icon)
    {
        int separatorIndex = icon.LastIndexOf('/');
        string name = separatorIndex >= 0 ? icon[(separatorIndex + 1)..] : icon;
        return name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }
}
