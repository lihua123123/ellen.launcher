// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.AvatarInfo.Factory.Builder;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.ViewModel.AvatarProperty;
using Ellen.Launcher.ViewModel.Wiki;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using EntityAvatarInfo = Ellen.Launcher.Model.Entity.AvatarInfo;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;
using MetadataWeapon = Ellen.Launcher.Model.Metadata.Weapon.Weapon;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

public sealed class SummaryAvatarFactory
{
    private readonly DetailedCharacter character;
    private readonly SummaryFactoryMetadataContext context;

    public SummaryAvatarFactory(SummaryFactoryMetadataContext context, EntityAvatarInfo avatarInfo)
    {
        ArgumentNullException.ThrowIfNull(avatarInfo.Info2);

        this.context = context;
        character = avatarInfo.Info2;
    }

    public static AvatarView Create(SummaryFactoryMetadataContext context, EntityAvatarInfo avatarInfo)
    {
        return new SummaryAvatarFactory(context, avatarInfo).Create();
    }

    public AvatarView Create()
    {
        MetadataAvatar avatar = context.GetAvatar(character.Base.Id);

        ProcessConstellations(
            avatar.SkillDepot,
            character.Constellations,
            out FrozenSet<SkillId> activatedConstellations,
            out FrozenDictionary<SkillId, SkillLevel> extraLevels);

        ImmutableArray<AvatarProperty> properties = character.SelectedProperties.SelectAsArray(FightPropertyFormat.ToAvatarProperty);
        properties.SortInPlace(InGameAvatarPropertyComparer.Shared);

        (double baseHp, double baseAttack, double baseDefense) = GetBaseValues(character.SelectedProperties);

        ImmutableArray<ReliquaryView> reliquaries = character.Relics.SelectAsArray(
            (relic, ctx) => SummaryReliquaryFactory.Create(ctx.context, relic, ctx.baseHp, ctx.baseAttack, ctx.baseDefense),
            (context: context, baseHp: baseHp, baseAttack: baseAttack, baseDefense: baseDefense));

        AvatarView propertyAvatar = new AvatarViewBuilder()
            .SetId(avatar.Id)
            .SetName(avatar.Name)
            .SetQuality(avatar.Quality)
            .SetNameCard(AvatarNameCardPicConverter.IconNameToUri(avatar.NameCard.PicturePrefix))
            .SetElement(ElementNameIconConverter.ElementNameToElementType(avatar.FetterInfo.VisionBefore))
            .SetConstellations(avatar.SkillDepot.Talents, activatedConstellations)
            .SetSkills(avatar.SkillDepot.CompositeSkillsNoInherents, character.Skills.ToFrozenDictionary(s => s.SkillId, s => s.Level), extraLevels)
            .SetFetterLevel(character.Base.Fetter)
            .SetProperties(properties)
            .SetLevelNumber(character.Base.Level)
            .SetWeapon(CreateWeapon(character.Weapon))
            .SetRecommendedProperties(character.RecommendRelicProperty.RecommendProperties)
            .SetReliquaries(reliquaries)
            .SetCostumeIconOrDefault(character, avatar)
            .SetPromoteLevel(character.Base.PromoteLevel)
            .View;

        return propertyAvatar;
    }

    /// <summary>
    /// 从战绩接口的面板属性（selected_properties）中取角色（含武器）的 HP/ATK/DEF 白值，
    /// 用于把固定值副词条折算成百分比。base 字段即白值，add 为绿字。
    /// <para>
    /// 注意面板用的是 2000 系列枚举（<see cref="FightProperty.FIGHT_PROP_MAX_HP"/>、
    /// <see cref="FightProperty.FIGHT_PROP_CUR_ATTACK"/>、<see cref="FightProperty.FIGHT_PROP_CUR_DEFENSE"/>），
    /// 而非圣遗物副词条用的 <see cref="FightProperty.FIGHT_PROP_HP"/> / <see cref="FightProperty.FIGHT_PROP_ATTACK"/> / <see cref="FightProperty.FIGHT_PROP_DEFENSE"/>。
    /// </para>
    /// </summary>
    private static (double BaseHp, double BaseAttack, double BaseDefense) GetBaseValues(ImmutableArray<BaseProperty> baseProperties)
    {
        if (baseProperties.IsDefaultOrEmpty)
        {
            return (0, 0, 0);
        }

        // 优先级：面板 2000 系列 > BASE_* 白值字段 > 小生命/小攻击/小防御字段。
        (double Value, int Priority) hp = (0, 0);
        (double Value, int Priority) attack = (0, 0);
        (double Value, int Priority) defense = (0, 0);

        foreach (BaseProperty property in baseProperties)
        {
            double baseValue = ParseStat(property.Base);
            double value = baseValue > 0
                ? baseValue
                : ParseStat(property.Final) - ParseStat(property.Add);

            switch (property.PropertyType)
            {
                case FightProperty.FIGHT_PROP_MAX_HP:
                    Consider(ref hp, value, 3);
                    break;
                case FightProperty.FIGHT_PROP_BASE_HP:
                    Consider(ref hp, value, 2);
                    break;
                case FightProperty.FIGHT_PROP_HP:
                    Consider(ref hp, value, 1);
                    break;

                case FightProperty.FIGHT_PROP_CUR_ATTACK:
                    Consider(ref attack, value, 3);
                    break;
                case FightProperty.FIGHT_PROP_BASE_ATTACK:
                    Consider(ref attack, value, 2);
                    break;
                case FightProperty.FIGHT_PROP_ATTACK:
                    Consider(ref attack, value, 1);
                    break;

                case FightProperty.FIGHT_PROP_CUR_DEFENSE:
                    Consider(ref defense, value, 3);
                    break;
                case FightProperty.FIGHT_PROP_BASE_DEFENSE:
                    Consider(ref defense, value, 2);
                    break;
                case FightProperty.FIGHT_PROP_DEFENSE:
                    Consider(ref defense, value, 1);
                    break;
            }
        }

        return (hp.Value, attack.Value, defense.Value);
    }

    private static void Consider(ref (double Value, int Priority) target, double value, int priority)
    {
        if (value > 0 && priority > target.Priority)
        {
            target = (value, priority);
        }
    }

    private static double ParseStat(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : 0;
    }

    private static void ProcessConstellations(
        SkillDepot depot,
        ImmutableArray<Constellation> dataConstellations,
        out FrozenSet<SkillId> activatedConstellationIds,
        out FrozenDictionary<SkillId, SkillLevel> extraLevels)
    {
        HashSet<SkillId> constellationIds = [];
        Dictionary<SkillId, SkillLevel> levels = [];

        // 达达利亚天赋：普攻技能等级+1
        if (depot.Inherents is [_, _, { } inherent] && inherent.GroupId == 3323U)
        {
            levels.Add(depot.CompositeSkillsNoInherents[0].Id, 1);
        }

        Verify.Operation(depot.Talents.Length == dataConstellations.Length, "Constellation count mismatch");
        foreach ((Model.Metadata.Avatar.Skill metaConstellation, Constellation dataConstellation) in depot.Talents.Zip(dataConstellations))
        {
            // Constellations are activated in order, so if the current constellation is
            // not activated, all the subsequent constellations will not be activated.
            if (!dataConstellation.IsActived)
            {
                break;
            }

            constellationIds.Add(dataConstellation.Id);

            if (metaConstellation.ExtraLevel is { } extraLevel)
            {
                int index = extraLevel.Index switch
                {
                    ExtraLevelIndexKind.NormalAttack => 0,
                    ExtraLevelIndexKind.ElementalSkill => 1,
                    ExtraLevelIndexKind.ElementalBurst => 2,
                    _ => throw EllenException.NotSupported("Unexpected extra level index."),
                };

                levels.Add(depot.CompositeSkillsNoInherents[index].Id, extraLevel.Level);
            }
        }

        activatedConstellationIds = constellationIds.ToFrozenSet();
        extraLevels = levels.ToFrozenDictionary();
    }

    private WeaponView CreateWeapon(DetailedWeapon detailedWeapon)
    {
        MetadataWeapon metadataWeapon = context.GetWeapon(detailedWeapon.Id);

        BaseValueInfoMetadataContext baseValueContext = new()
        {
            GrowCurveMap = context.LevelDictionaryWeaponGrowCurveMap,
            PromoteMap = context.IdDictionaryWeaponLevelPromoteMap[metadataWeapon.PromoteId],
        };

        ImmutableArray<NameValue<string>> baseValues = BaseValueInfoConverter.ToNameValues(metadataWeapon.GrowCurves.ToPropertyCurveValues(), detailedWeapon.Level, detailedWeapon.PromoteLevel, baseValueContext);

        return new WeaponViewBuilder()
            .SetName(metadataWeapon.Name)
            .SetIcon(EquipIconConverter.IconNameToUri(metadataWeapon.Icon))
            .SetDescription(metadataWeapon.Description)
            .SetLevel(LevelFormat.Format(detailedWeapon.Level))
            .SetQuality(metadataWeapon.Quality)
            .SetEquipType(EquipType.EQUIP_WEAPON)
            .SetId(metadataWeapon.Id)
            .SetLevelNumber(detailedWeapon.Level)
            .SetMainProperty(baseValues.ElementAtOrDefault(0))
            .SetSubProperty(baseValues.ElementAtOrDefault(1))
            .SetAffixLevelNumber(detailedWeapon.AffixLevel)
            .SetAffixName(metadataWeapon.Affix?.Name)
            .SetAffixDescription(metadataWeapon.Affix?.Descriptions.Single(a => a.Level == (detailedWeapon.AffixLevel - 1)).Description)
            .SetWeaponType(metadataWeapon.WeaponType)
            .SetPromoteLevel(detailedWeapon.PromoteLevel)
            .View;
    }
}