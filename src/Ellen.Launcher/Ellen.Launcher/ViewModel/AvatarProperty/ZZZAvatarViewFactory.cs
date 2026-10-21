// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.AvatarInfo.Factory;
using Ellen.Launcher.Web.Endpoint.Upstream;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;
using System.Collections.Immutable;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

/// <summary>
/// 把米游社绝区零战绩数据映射到现有的 <see cref="AvatarView"/>，
/// 以直接复用「我的角色」页已有的网格 / 列表 / 详情 UI。
/// </summary>
public static class ZZZAvatarViewFactory
{
    public static AvatarView Create(ZZZFullAgent agent, ImmutableArray<Uri> skillIcons = default)
    {
        Uri icon = ToUri(agent.RoleSquareUrl, StaticResourcesEndpoints.UIIconNone);
        Uri banner = ToUri(agent.BannerIcon, icon);
        QualityType quality = ToQuality(agent.Rarity);
        ImmutableArray<ReliquaryView> reliquaries = [.. agent.Discs.Select(disc => ToReliquaryView(disc, agent))];

        AvatarView view = new AvatarView
        {
            Id = (uint)agent.Id,
            Name = agent.Name,
            Icon = icon,
            SideIcon = icon,
            NameCard = banner,
            Quality = quality,
            Element = ToElement(agent.ElementType),
            LevelNumber = (uint)agent.Level,
            FetterLevel = 0,
            PromoteArray = [],
            PromoteLevel = 0,
            Constellations = [.. agent.Ranks.OrderBy(static rank => rank.Position).Select((rank, index) => ToConstellationView(rank, index))],
            Skills = BuildSkills(agent.Skills, skillIcons),
            Properties = [.. agent.Properties.Select(ToAvatarProperty).OfType<AvatarProperty>()],
            Weapon = agent.Weapon is { } weapon ? ToWeaponView(weapon) : null,
            Reliquaries = reliquaries,
            ReliquaryItems = [.. reliquaries],
            SummaryCard = ReliquarySubPropertySummaryFactory.Compute(reliquaries),
            RecommendedProperties = new RecommendPropertiesView
            {
                SandProperties = [],
                GobletProperties = [],
                CircletProperties = [],
                SubProperties = [],
            },
            IsMetadataSynthesized = false,
        };

        return view;
    }

    /// <summary>
    /// 详情接口失败时的兜底：至少展示姓名、等级、稀有度、图标与元素。
    /// </summary>
    public static AvatarView CreateBasic(ZZZAvatarBasic agent)
    {
        Uri icon = ToUri(agent.RoleSquareUrl, StaticResourcesEndpoints.UIIconNone);
        return new AvatarView
        {
            Id = (uint)agent.Id,
            Name = agent.Name,
            Icon = icon,
            SideIcon = icon,
            NameCard = icon,
            Quality = ToQuality(agent.Rarity),
            Element = ToElement(agent.ElementType),
            LevelNumber = (uint)agent.Level,
            FetterLevel = 0,
            PromoteArray = [],
            PromoteLevel = 0,
            Constellations = [],
            Skills = [],
            Properties =
            [
                new AvatarProperty(FightProperty.FIGHT_PROP_MAX_HP, "生命值上限", "0"),
                new AvatarProperty(FightProperty.FIGHT_PROP_CUR_ATTACK, "攻击力", "0"),
                new AvatarProperty(FightProperty.FIGHT_PROP_CUR_DEFENSE, "防御力", "0"),
            ],
            Weapon = null,
            Reliquaries = [],
            ReliquaryItems = [],
            RecommendedProperties = new RecommendPropertiesView
            {
                SandProperties = [],
                GobletProperties = [],
                CircletProperties = [],
                SubProperties = [],
            },
            IsMetadataSynthesized = true,
        };
    }

    private static WeaponView ToWeaponView(ZZZWeapon weapon)
    {
        Uri icon = ToUri(weapon.Icon, StaticResourcesEndpoints.UIIconNone);
        return new WeaponView
        {
            Id = (uint)weapon.Id,
            Name = weapon.Name,
            Description = weapon.TalentContent,
            Icon = icon,
            Quality = ToQuality(weapon.Rarity),
            Level = weapon.Level.ToString(System.Globalization.CultureInfo.CurrentCulture),
            LevelNumber = (uint)weapon.Level,
            AffixLevelNumber = (uint)weapon.Star,
            AffixName = weapon.TalentTitle,
            AffixDescription = weapon.TalentContent,
            MainProperty = weapon.MainProperties.FirstOrDefault() is { } main
                ? new NameValue<string>(main.Name, PropertyValue(main))
                : null,
            SubProperty = weapon.Properties.FirstOrDefault() is { } sub
                ? new NameValue<string>(sub.Name, PropertyValue(sub))
                : null,
            EquipType = EquipType.EQUIP_WEAPON,
            WeaponType = WeaponType.WEAPON_NONE,
            PromoteArray = [],
            PromoteLevel = 0,
        };
    }

    private static ReliquaryView ToReliquaryView(ZZZDisc disc, ZZZFullAgent agent)
    {
        Uri icon = ToUri(disc.Icon, StaticResourcesEndpoints.UIIconNone);
        return new ReliquaryView
        {
            Name = disc.Name,
            Description = disc.SetEffect?.Name ?? string.Empty,
            Icon = icon,
            Quality = ToQuality(disc.Rarity),
            Level = disc.Level.ToString(System.Globalization.CultureInfo.CurrentCulture),
            SetName = disc.SetEffect?.Name ?? string.Empty,
            MainProperty = disc.MainProperties.FirstOrDefault() is { } main
                ? new NameValue<string>(StatName(main.PropertyId, main.Name), PropertyValue(main))
                : null,
            EquipType = ToEquipType(disc.EquipmentType),
            ComposedSubProperties = [.. disc.Properties.Select(property => ToComposedSubProperty(property, disc.Rarity, agent))],
        };
    }

    private static ReliquaryComposedSubProperty ToComposedSubProperty(ZZZProperty property, string rarity, ZZZFullAgent agent)
    {
        FightProperty fightProperty = ToFightProperty(property.PropertyId) ?? FightProperty.FIGHT_PROP_NONE;
        string value = PropertyValue(property);
        double rolls = CalculateRolls(property.PropertyId, value, rarity, agent);
        uint enhancedCount = (uint)Math.Max(1, (int)ParseNumber(ToText(property.Add)) + 1);
        return new ReliquaryComposedSubProperty(
            fightProperty,
            value,
            enhancedCount,
            rolls,
            StatName(property.PropertyId, property.Name))
        {
            EnhancementCountFormat = "0.0",
        };
    }

    private static ConstellationView ToConstellationView(ZZZRank rank, int index)
    {
        return new ConstellationView
        {
            Name = rank.Name,
            Description = rank.Description,
            Icon = StaticResourcesEndpoints.UIIconNone,
            IsActivated = rank.IsUnlocked,
            Number = index + 1,
            ShowNumber = true,
        };
    }

    private static ImmutableArray<SkillView> BuildSkills(List<ZZZSkill> skills, ImmutableArray<Uri> icons)
    {
        ZZZSkill[] ordered = [.. skills.OrderBy(static skill => GetSkillOrder(skill.SkillType))];
        return
        [
            .. ordered.Select((skill, index) => ToSkillView(
                skill,
                index < icons.Length ? icons[index] : StaticResourcesEndpoints.UIIconNone)),
        ];
    }

    private static int GetSkillOrder(int skillType)
    {
        return skillType switch
        {
            0 => 0, // 普攻
            2 => 1, // 闪避
            6 => 2, // 支援技
            1 => 3, // 特殊技
            3 => 4, // 连携技
            5 => 5, // 核心技
            _ => 99,
        };
    }

    private static SkillView ToSkillView(ZZZSkill skill, Uri icon)
    {
        string name = skill.Items.FirstOrDefault()?.Title ?? skill.SkillType.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string description = string.Join("\n", skill.Items.Select(static item => item.Text));
        return new SkillView
        {
            Name = name,
            Description = description,
            Icon = icon,
            Level = skill.Level.ToString(System.Globalization.CultureInfo.CurrentCulture),
            LevelNumber = (uint)skill.Level,
        };
    }

    private static AvatarProperty? ToAvatarProperty(ZZZProperty property)
    {
        FightProperty fightProperty = ToFightProperty(property.PropertyId) ?? FightProperty.FIGHT_PROP_NONE;
        return new AvatarProperty(
            fightProperty,
            StatName(property.PropertyId, property.Name),
            PropertyValue(property),
            string.IsNullOrEmpty(ToText(property.Add)) ? null : ToText(property.Add));
    }

    private static FightProperty? ToFightProperty(int propertyId)
    {
        return propertyId switch
        {
            1 or 11101 or 11102 or 11103 => FightProperty.FIGHT_PROP_MAX_HP,
            2 or 12101 or 12102 or 12103 => FightProperty.FIGHT_PROP_CUR_ATTACK,
            3 or 13101 or 13102 or 13103 => FightProperty.FIGHT_PROP_CUR_DEFENSE,
            5 or 20103 => FightProperty.FIGHT_PROP_CRITICAL,
            6 or 21103 => FightProperty.FIGHT_PROP_CRITICAL_HURT,
            7 or 31203 => FightProperty.FIGHT_PROP_ELEMENT_MASTERY,
            11 or 30502 => FightProperty.FIGHT_PROP_CHARGE_EFFICIENCY,
            31503 => FightProperty.FIGHT_PROP_PHYSICAL_ADD_HURT,
            31603 => FightProperty.FIGHT_PROP_FIRE_ADD_HURT,
            31703 => FightProperty.FIGHT_PROP_ICE_ADD_HURT,
            31803 => FightProperty.FIGHT_PROP_ELEC_ADD_HURT,
            31903 => FightProperty.FIGHT_PROP_WATER_ADD_HURT,
            32303 => FightProperty.FIGHT_PROP_WIND_ADD_HURT,
            _ => null,
        };
    }

    private static QualityType ToQuality(string rarity)
    {
        return rarity switch
        {
            "S" => QualityType.QUALITY_ORANGE,
            "A" => QualityType.QUALITY_PURPLE,
            "B" => QualityType.QUALITY_BLUE,
            _ => QualityType.QUALITY_NONE,
        };
    }

    private static string StatName(int propertyId, string? fallback)
    {
        return propertyId switch
        {
            1 => "生命值",
            2 => "攻击力",
            3 => "防御力",
            4 or 12202 => "冲击力",
            5 => "暴击率",
            6 => "暴击伤害",
            7 => "异常精通",
            8 => "异常掌控",
            9 or 23103 => "穿透率",
            11 => "能量自动回复",
            232 or 23203 => "穿透值",
            11101 or 11102 or 11103 => "生命值",
            12101 or 12102 or 12103 => "攻击力",
            13101 or 13102 or 13103 => "防御力",
            20103 => "暴击率",
            21103 => "暴击伤害",
            31203 => "异常精通",
            31402 => "异常掌控",
            30502 => "能量自动回复",
            315 or 31503 => "物理伤害加成",
            316 or 31603 => "火属性伤害加成",
            317 or 31703 => "冰属性伤害加成",
            318 or 31803 => "电属性伤害加成",
            319 or 31903 => "以太伤害加成",
            323 or 32303 => "风属性伤害加成",
            _ => fallback ?? string.Empty,
        };
    }

    /// <summary>
    /// ZZZ 副词条一次强化的基准值（S 级）。实际词条数 = 当前数值 / 单次强化值。
    /// A 级约为 S 的 2/3，B 级约为 1/3。
    /// </summary>
    private static double CalculateRolls(int propertyId, string text, string rarity, ZZZFullAgent agent)
    {
        double value = ParseNumber(text);
        if (value <= 0)
        {
            return 0;
        }

        double rarityMultiplier = rarity switch
        {
            "S" => 1.0,
            "A" => 2.0 / 3.0,
            "B" => 1.0 / 3.0,
            _ => 1.0,
        };

        // 固定生命/攻击/防御沿用原神规则：先按角色白值折算成百分比，再除以百分比单次强化值。
        if (propertyId is 11103 or 12103 or 13103)
        {
            int basePropertyId = propertyId switch
            {
                11103 => 1,
                12103 => 2,
                _ => 3,
            };
            double baseValue = GetFinalStat(agent, basePropertyId);
            if (baseValue <= 0)
            {
                return 0;
            }

            double percent = value / baseValue * 100.0;
            double percentUnit = propertyId switch
            {
                11103 => 3.0,
                12103 => 3.0,
                _ => 4.8,
            };
            return percent / (percentUnit * rarityMultiplier);
        }

        double unit = propertyId switch
        {
            20103 => 2.4,
            21103 => 4.8,
            11102 or 12102 => 3.0,
            13102 => 4.8,
            31203 => 9.0,
            23203 => 9.0,
            _ => 0.0,
        };

        return unit <= 0 ? 0 : value / (unit * rarityMultiplier);
    }

    private static double GetFinalStat(ZZZFullAgent agent, int propertyId)
    {
        ZZZProperty? property = agent.Properties.FirstOrDefault(item => item.PropertyId == propertyId);
        if (property is null)
        {
            return 0;
        }

        double finalValue = ParseNumber(PropertyValue(property));
        return finalValue > 0 ? finalValue : ParseNumber(ToText(property.Base));
    }

    private static double ParseNumber(string text)
    {
        string trimmed = text.Trim().TrimEnd('%');
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : 0;
    }

    private static ElementType ToElement(int elementType)
    {
        return elementType switch
        {
            201 => ElementType.Fire,
            202 => ElementType.Ice,
            203 => ElementType.Electric,
            204 => ElementType.Wind,
            _ => ElementType.None,
        };
    }

    private static EquipType ToEquipType(int equipmentType)
    {
        return equipmentType switch
        {
            1 => EquipType.EQUIP_BRACER,
            2 => EquipType.EQUIP_NECKLACE,
            3 => EquipType.EQUIP_SHOES,
            4 => EquipType.EQUIP_RING,
            5 => EquipType.EQUIP_DRESS,
            6 => EquipType.EQUIP_WEAPON,
            _ => EquipType.EQUIP_NONE,
        };
    }

    private static Uri ToUri(string? value, Uri fallback)
    {
        return string.IsNullOrEmpty(value) ? fallback : value.ToUri();
    }

    private static string ToText(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => element.GetString() ?? string.Empty,
            System.Text.Json.JsonValueKind.Number => element.GetRawText(),
            System.Text.Json.JsonValueKind.True => bool.TrueString,
            System.Text.Json.JsonValueKind.False => bool.FalseString,
            _ => string.Empty,
        };
    }

    private static string PropertyValue(ZZZProperty property)
    {
        string text = ToText(property.Final);
        if (string.IsNullOrEmpty(text))
        {
            text = ToText(property.Base);
        }

        if (string.IsNullOrEmpty(text))
        {
            text = ToText(property.Add);
        }

        return text;
    }
}
