// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Service.AvatarInfo.Factory;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CalculateConsumption = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.Consumption;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;
using MetadataWeapon = Ellen.Launcher.Model.Metadata.Weapon.Weapon;
using static Ellen.Launcher.Service.Cultivation.Offline.Lookups;

namespace Ellen.Launcher.Service.Cultivation.Offline;

public static class OfflineCalculator
{
    public static BatchConsumption CalculateWikiAvatarConsumption(AvatarPromotionDelta delta, MetadataAvatar avatar)
    {
        return BatchConsumption.CreateForWiki(CalculateAvatarConsumption(delta, avatar));
    }

    public static BatchConsumption CalculateWikiWeaponConsumption(AvatarPromotionDelta delta, MetadataWeapon weapon)
    {
        return BatchConsumption.CreateForWiki(CalculateWeaponConsumption(delta, weapon));
    }

    public static BatchConsumption CalculateBatchConsumption(AvatarPromotionDelta delta, SummaryFactoryMetadataContext context)
    {
        return CalculateBatchConsumption([delta], context);
    }

    public static BatchConsumption CalculateBatchConsumption(ImmutableArray<AvatarPromotionDelta> deltas, SummaryFactoryMetadataContext context)
    {
        ImmutableArray<CalculateConsumption>.Builder consumptions = ImmutableArray.CreateBuilder<CalculateConsumption>(deltas.Length);
        foreach (AvatarPromotionDelta delta in deltas)
        {
            ArgumentNullException.ThrowIfNull(delta.Weapon);

            if (context.GetAvatar(delta.AvatarId) is not { } avatar || context.GetWeapon(delta.Weapon.Id) is not { } weapon)
            {
                continue;
            }

            CalculateConsumption consumption = new()
            {
                // Since we will merge skill consume into avatar consume when saving to db
                // We just calculate them altogether.
                AvatarConsume = CalculateAvatarConsumption(delta, avatar),
                AvatarSkillConsume = [],
                WeaponConsume = CalculateWeaponConsumption(delta, weapon),
            };

            consumptions.Add(consumption);
        }

        return BatchConsumption.CreateForBatch(consumptions.ToImmutable());
    }

    private static ImmutableArray<Item> CalculateAvatarConsumption(AvatarPromotionDelta delta, MetadataAvatar avatar)
    {
        ElementType element = ElementNameIconConverter.ElementNameToElementType(avatar.FetterInfo.Vision);

        return CalculateCustomAvatarConsumption(delta, new()
        {
            GemBaseId = ElementGemBaseIds[element],
            BossMaterialId = avatar.CultivationItems.Length > 1 ? avatar.CultivationItems[1] : 0,
            SpecialtyId = avatar.CultivationItems.Length > 2 ? avatar.CultivationItems[2] : 0,
            TalentBookIds = avatar.CultivationItems.Length > 4 ? CreateSeries(avatar.CultivationItems[4], 3) : [],
            MonsterMaterialIds = avatar.CultivationItems.Length > 3 ? CreateSeries(avatar.CultivationItems[3], 3) : [],
            WeeklyBossMaterialId = avatar.CultivationItems.Length > 5 ? avatar.CultivationItems[5] : 0,
        });
    }

    private static ImmutableArray<Item> CalculateWeaponConsumption(AvatarPromotionDelta delta, MetadataWeapon weapon)
    {
        if (delta.Weapon is null)
        {
            return [];
        }

        return CalculateCustomWeaponConsumption(delta, new()
        {
            Quality = weapon.RankLevel,
            AscensionMaterialSeries = weapon.CultivationItems.Length >= 3
                ? [CreateSeries(weapon.CultivationItems[0], 4), CreateSeries(weapon.CultivationItems[1], 4), CreateSeries(weapon.CultivationItems[2], 4)]
                : [],
        });
    }

    /// <summary>由最高品质 ID 构造一个从低到高的连续品质系列。</summary>
    private static ImmutableArray<uint> CreateSeries(uint highestId, int length)
    {
        if (highestId is 0)
        {
            return [];
        }

        ImmutableArray<uint>.Builder builder = ImmutableArray.CreateBuilder<uint>(length);
        for (int i = length - 1; i >= 0; i--)
        {
            builder.Add(highestId - (uint)i);
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// 自定义材料计划（角色）：等级决定数量，材料种类由用户选择。
    /// 摩拉 / 经验书 / 智识之冕仍按模板算，其余材料按用户选中的 ID 代入既有消耗表。
    /// </summary>
    public static ImmutableArray<Item> CalculateCustomAvatarConsumption(AvatarPromotionDelta delta, CustomAvatarMaterialSelection materials)
    {
        Dictionary<uint, uint> itemCounts = [];

        // 计算 95 / 100 级上限突破
        if (delta is { AvatarLevelCurrent: <= 90, AvatarLevelTarget: >= 95U })
        {
            AddOrUpdateItem(itemCounts, MaterialIds.MasterlessStellaFortuna, 1U);
        }

        if (delta is { AvatarLevelCurrent: <= 95, AvatarLevelTarget: >= 100U })
        {
            AddOrUpdateItem(itemCounts, MaterialIds.MasterlessStellaFortuna, 2U);
        }

        // 经验书与摩拉按模板算
        if (delta.AvatarLevelCurrent < delta.AvatarLevelTarget)
        {
            CalculateAvatarExperience(delta, itemCounts);
        }

        if (delta.AvatarLevelCurrent <= delta.AvatarLevelTarget)
        {
            CalculateCustomAvatarAscension(delta, itemCounts, materials);
        }

        if (delta.SkillList.Length > 0)
        {
            CalculateCustomAvatarTalents(delta, itemCounts, materials);
        }

        return ConvertToItems(itemCounts);
    }

    /// <summary>
    /// 自定义材料计划（武器）：武器品质与等级决定数量，突破材料种类由用户选择。
    /// 摩拉 / 精锻用魔矿按模板算。
    /// </summary>
    public static ImmutableArray<Item> CalculateCustomWeaponConsumption(AvatarPromotionDelta delta, CustomWeaponMaterialSelection materials)
    {
        if (delta.Weapon is null)
        {
            return [];
        }

        Dictionary<uint, uint> itemCounts = [];

        if (delta.Weapon.LevelCurrent < delta.Weapon.LevelTarget)
        {
            CalculateWeaponExperience(materials.Quality, delta.Weapon, itemCounts);
        }

        if (delta.Weapon.LevelCurrent <= delta.Weapon.LevelTarget)
        {
            CalculateCustomWeaponAscension(delta.Weapon, itemCounts, materials.Quality, materials.AscensionMaterialSeries);
        }

        return ConvertToItems(itemCounts);
    }

    private static void CalculateAvatarExperience(AvatarPromotionDelta delta, Dictionary<uint, uint> itemCounts)
    {
        int requiredExp = AvatarLevelExperience.CalculateTotalExperience((int)delta.AvatarLevelCurrent, (int)delta.AvatarLevelTarget);
        uint expItemCount = (uint)Math.Ceiling((double)requiredExp / PurpleExpBookPerItemExp);
        AddOrUpdateItem(itemCounts, MaterialIds.Mora, expItemCount * PurpleExpBookPerItemMora);
        AddOrUpdateItem(itemCounts, MaterialIds.HeroesWit, expItemCount);
    }

    private static void CalculateCustomAvatarAscension(AvatarPromotionDelta delta, Dictionary<uint, uint> itemCounts, CustomAvatarMaterialSelection materials)
    {
        ImmutableArray<int> requiredAscensions = Ascension.GetRequiredLevels(AscensionLevels, delta);
        if (requiredAscensions.IsEmpty)
        {
            return;
        }

        foreach (int requiredAscension in requiredAscensions)
        {
            int ascensionIndex = AvatarAscensionIndices[requiredAscension];

            // 摩拉与经验书按模板算；材料种类来自用户选择，数量仍用同一张消耗表
            AddOrUpdateItem(itemCounts, MaterialIds.Mora, (uint)AvatarAscensionMoraCosts[ascensionIndex]);
            AddOrUpdateItem(itemCounts, AvatarGemConsumption[ascensionIndex].Positive(materials.GemBaseId));
            AddOrUpdateItem(itemCounts, materials.BossMaterialId, AvatarBossMaterialCounts[ascensionIndex]);
            AddOrUpdateItem(itemCounts, materials.SpecialtyId, AvatarSpecialtyCounts[ascensionIndex]);
            AddOrUpdateItem(itemCounts,
                CustomAvatarMaterialSelection.GetIdByQualityOffset(materials.MonsterMaterialIds, AvatarMonsterMaterialConsumption[ascensionIndex].QualityOffset),
                AvatarMonsterMaterialConsumption[ascensionIndex].Count);
        }
    }

    private static void CalculateCustomAvatarTalents(AvatarPromotionDelta delta, Dictionary<uint, uint> itemCounts, CustomAvatarMaterialSelection materials)
    {
        foreach (PromotionDelta skill in delta.SkillList)
        {
            if (skill.LevelCurrent >= skill.LevelTarget)
            {
                continue;
            }

            CalculateCustomAvatarTalent(skill, itemCounts, materials);
        }
    }

    private static void CalculateCustomAvatarTalent(PromotionDelta delta, Dictionary<uint, uint> itemCounts, CustomAvatarMaterialSelection materials)
    {
        uint currentLevel = delta.LevelCurrent;
        uint targetLevel = delta.LevelTarget;

        if (currentLevel >= targetLevel || currentLevel >= 10 || targetLevel > 10)
        {
            return;
        }

        foreach (int upgradeIndex in Enumerable.Sequence((int)currentLevel, (int)(targetLevel - 1), 1))
        {
            // 摩拉与智识之冠按模板算；材料种类来自用户选择，按品质偏移从系列直接取 ID
            AddOrUpdateItem(itemCounts, MaterialIds.Mora, (uint)TalentMoraCosts[upgradeIndex]);
            AddOrUpdateItem(itemCounts,
                CustomAvatarMaterialSelection.GetIdByQualityOffset(materials.TalentBookIds, TalentBookConsumption[upgradeIndex].QualityOffset),
                TalentBookConsumption[upgradeIndex].Count);
            AddOrUpdateItem(itemCounts,
                CustomAvatarMaterialSelection.GetIdByQualityOffset(materials.MonsterMaterialIds, TalentMonsterMaterialConsumption[upgradeIndex].QualityOffset),
                TalentMonsterMaterialConsumption[upgradeIndex].Count);
            AddOrUpdateItem(itemCounts, materials.WeeklyBossMaterialId, WeeklyBossCounts[upgradeIndex]);

            if (upgradeIndex is 9)
            {
                AddOrUpdateItem(itemCounts, MaterialIds.CrownOfInsight, 1U);
            }
        }
    }

    private static void CalculateWeaponExperience(QualityType quality, PromotionDelta delta, Dictionary<uint, uint> itemCounts)
    {
        int requiredExp = WeaponLevelExperience.CalculateTotalExperience(quality, (int)delta.LevelCurrent, (int)delta.LevelTarget);
        uint expItemCount = (uint)Math.Ceiling((double)requiredExp / PurpleWeaponOrePerItemExp);
        AddOrUpdateItem(itemCounts, MaterialIds.Mora, expItemCount * PurpleWeaponOrePerItemMora);
        AddOrUpdateItem(itemCounts, MaterialIds.MysticEnhancementOre, expItemCount);
    }

    private static void CalculateCustomWeaponAscension(PromotionDelta delta, Dictionary<uint, uint> itemCounts, QualityType quality, ImmutableArray<ImmutableArray<uint>> materialSeries)
    {
        if (materialSeries.Length < 3)
        {
            return;
        }

        ImmutableArray<int> requiredAscensions = Ascension.GetRequiredLevels(AscensionLevels, delta);
        Debug.Assert(quality >= QualityType.QUALITY_BLUE || !requiredAscensions.Contains(80));

        if (requiredAscensions.IsEmpty)
        {
            return;
        }

        foreach (int requiredAscension in requiredAscensions)
        {
            int ascensionIndex = AscensionLevels.IndexOf(requiredAscension);

            // 摩拉按模板算；三个突破槽的材料种类来自用户选择，按品质偏移从各自系列直接取 ID
            AddOrUpdateItem(itemCounts, MaterialIds.Mora, (uint)WeaponAscensionMoraCosts[quality][ascensionIndex]);
            AddOrUpdateItem(itemCounts,
                CustomWeaponMaterialSelection.GetIdByQualityOffset(materialSeries[0], WeaponMaterialConsumption[quality][ascensionIndex].QualityOffset),
                WeaponMaterialConsumption[quality][ascensionIndex].Count);
            AddOrUpdateItem(itemCounts,
                CustomWeaponMaterialSelection.GetIdByQualityOffset(materialSeries[1], WeaponMonsterMaterialAConsumption[quality][ascensionIndex].QualityOffset),
                WeaponMonsterMaterialAConsumption[quality][ascensionIndex].Count);
            AddOrUpdateItem(itemCounts,
                CustomWeaponMaterialSelection.GetIdByQualityOffset(materialSeries[2], WeaponMonsterMaterialBConsumption[quality][ascensionIndex].QualityOffset),
                WeaponMonsterMaterialBConsumption[quality][ascensionIndex].Count);
        }
    }

    private static void AddOrUpdateItem(Dictionary<uint, uint> items, uint id, uint count)
    {
        // 自定义计划里可能有未选材料的空槽，MaterialId 0 不是有效材料，直接跳过
        if (count is 0 || id is 0)
        {
            return;
        }

        CollectionsMarshal.GetValueRefOrAddDefault(items, id, out _) += count;
    }

    private static void AddOrUpdateItem(Dictionary<uint, uint> items, (uint Id, uint Count) tuple)
    {
        AddOrUpdateItem(items, tuple.Id, tuple.Count);
    }

    private static ImmutableArray<Item> ConvertToItems(Dictionary<uint, uint> itemCounts)
    {
        if (itemCounts.Count is 0)
        {
            return [];
        }

        ImmutableArray<Item>.Builder builder = ImmutableArray.CreateBuilder<Item>(itemCounts.Count);
        foreach ((uint id, uint count) in itemCounts)
        {
            builder.Add(new()
            {
                Id = id,
                Num = count,
            });
        }

        return builder.ToImmutable();
    }
}