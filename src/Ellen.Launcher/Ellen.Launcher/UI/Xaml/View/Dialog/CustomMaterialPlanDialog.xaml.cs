// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.Cultivation.Offline;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using static Ellen.Launcher.Service.Cultivation.Offline.Lookups;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

/// <summary>
/// 自定义材料计划弹窗：
/// <para>角色侧按 7 种元素选择晶石（必填），天赋 / 怪物 / 周本 / 特产 / Boss 材料可留空；</para>
/// <para>武器侧按品质 + 等级，三类突破材料（武器本 + 两种怪物）可留空；</para>
/// <para>有品质分级的材料按连续 ID 归为一个系列，候选只显示一个条目（图标 + 名称串联），数量仍由等级模板算表推导。</para>
/// </summary>
public sealed partial class CustomMaterialPlanDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly IMetadataService metadataService;

    // 原质(112001)与无主的命星(104300)不是任何养成材料，需排除。
    private const uint ProtoMaterialId = 112001U;
    private const uint CrownMaterialId = 104319U;

    [GeneratedConstructor(InitializeComponent = true)]
    public partial CustomMaterialPlanDialog(IServiceProvider serviceProvider);

    public int SelectedCategoryIndex { get; set; }

    public double AvatarLevelCurrent { get; set; } = 1;
    public double AvatarLevelTarget { get; set; } = 90;
    public double SkillALevelCurrent { get; set; } = 1;
    public double SkillALevelTarget { get; set; } = 10;
    public double SkillELevelCurrent { get; set; } = 1;
    public double SkillELevelTarget { get; set; } = 10;
    public double SkillQLevelCurrent { get; set; } = 1;
    public double SkillQLevelTarget { get; set; } = 10;
    public double WeaponLevelCurrent { get; set; } = 1;
    public double WeaponLevelTarget { get; set; } = 90;

    public ObservableCollection<ElementChoice> ElementChoices { get; } = [];
    public ObservableCollection<MaterialChoice> BossChoices { get; } = [];
    public ObservableCollection<MaterialChoice> SpecialtyChoices { get; } = [];
    public ObservableCollection<SeriesChoice> TalentBookChoices { get; } = [];
    public ObservableCollection<SeriesChoice> AvatarMonsterChoices { get; } = [];
    public ObservableCollection<MaterialChoice> WeeklyBossChoices { get; } = [];
    public ObservableCollection<SeriesChoice> WeaponAscensionChoices { get; } = [];
    public ObservableCollection<SeriesChoice> WeaponMonster1Choices { get; } = [];
    public ObservableCollection<SeriesChoice> WeaponMonster2Choices { get; } = [];
    public ObservableCollection<QualityChoice> QualityChoices { get; } = [];

    public ElementChoice? SelectedElement { get; set; }
    public MaterialChoice? SelectedBoss { get; set; }
    public MaterialChoice? SelectedSpecialty { get; set; }
    public SeriesChoice? SelectedTalentBook { get; set; }
    public SeriesChoice? SelectedAvatarMonster { get; set; }
    public MaterialChoice? SelectedWeeklyBoss { get; set; }
    public SeriesChoice? SelectedWeaponAscension { get; set; }
    public SeriesChoice? SelectedWeaponMonster1 { get; set; }
    public SeriesChoice? SelectedWeaponMonster2 { get; set; }
    public QualityChoice? SelectedQuality { get; set; }

    public async ValueTask<ValueResult<bool, CustomMaterialPlanResult>> SelectAsync()
    {
        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return new(false, default!);
        }

        CultivationMetadataContext context = await metadataService
            .GetContextAsync<CultivationMetadataContext>()
            .ConfigureAwait(false);

        await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
        InitializeChoices(context);

        SelectedElement = ElementChoices.FirstOrDefault();
        SelectedQuality = QualityChoices.LastOrDefault();
        Bindings.Update();

        if (await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false) is not ContentDialogResult.Primary)
        {
            return new(false, default!);
        }

        await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();

        // 元素必填（晶石是角色养成的基础），其余留空则不添加该条目。
        if (SelectedElement is null && SelectedBoss is null && SelectedSpecialty is null
            && SelectedTalentBook is null && SelectedAvatarMonster is null && SelectedWeeklyBoss is null
            && SelectedWeaponAscension is null && SelectedWeaponMonster1 is null && SelectedWeaponMonster2 is null)
        {
            return new(false, default!);
        }

        CustomAvatarMaterialSelection avatarSelection = BuildAvatarSelection();
        CustomWeaponMaterialSelection weaponSelection = BuildWeaponSelection();

        return new(true, new(
            avatarSelection.IsConfigured ? BuildAvatarDelta() : null,
            avatarSelection.IsConfigured ? avatarSelection : null,
            weaponSelection.IsConfigured ? BuildWeaponDelta() : null,
            weaponSelection.IsConfigured ? weaponSelection : null));
    }

    private AvatarPromotionDelta BuildAvatarDelta()
    {
        uint current = Clamp(AvatarLevelCurrent, 1, 100);
        uint target = Clamp(AvatarLevelTarget, 1, 100);

        return new()
        {
            AvatarId = 1U,
            AvatarLevelCurrent = current,
            AvatarLevelTarget = target,
            AvatarPromoteLevel = (uint)BaseValueInfoConverter.GetPromoteLevel(current, 100U, true),
            SkillList =
            [
                new PromotionDelta { Id = 1U, LevelCurrent = Clamp(SkillALevelCurrent, 1, 10), LevelTarget = Clamp(SkillALevelTarget, 1, 10) },
                new PromotionDelta { Id = 2U, LevelCurrent = Clamp(SkillELevelCurrent, 1, 10), LevelTarget = Clamp(SkillELevelTarget, 1, 10) },
                new PromotionDelta { Id = 3U, LevelCurrent = Clamp(SkillQLevelCurrent, 1, 10), LevelTarget = Clamp(SkillQLevelTarget, 1, 10) },
            ],
        };
    }

    private AvatarPromotionDelta BuildWeaponDelta()
    {
        uint current = Clamp(WeaponLevelCurrent, 1, 90);
        uint target = Clamp(WeaponLevelTarget, 1, 90);

        return new()
        {
            Weapon = new PromotionDelta
            {
                Id = 1U,
                LevelCurrent = current,
                LevelTarget = target,
                WeaponPromoteLevel = (uint)BaseValueInfoConverter.GetPromoteLevel(current, 90U, true),
            },
        };
    }

    private CustomAvatarMaterialSelection BuildAvatarSelection()
    {
        return new()
        {
            GemBaseId = SelectedElement is { } element && ElementGemBaseIds.TryGetValue(element.Element, out uint gemId) ? gemId : 0U,
            BossMaterialId = SelectedBoss?.Id ?? 0U,
            SpecialtyId = SelectedSpecialty?.Id ?? 0U,
            TalentBookIds = SelectedTalentBook?.Ids ?? [],
            MonsterMaterialIds = SelectedAvatarMonster?.Ids ?? [],
            WeeklyBossMaterialId = SelectedWeeklyBoss?.Id ?? 0U,
        };
    }

    private CustomWeaponMaterialSelection BuildWeaponSelection()
    {
        return new()
        {
            Quality = SelectedQuality?.Quality ?? QualityType.QUALITY_ORANGE,
            AscensionMaterialSeries =
            [
                SelectedWeaponAscension?.Ids ?? [],
                SelectedWeaponMonster1?.Ids ?? [],
                SelectedWeaponMonster2?.Ids ?? [],
            ],
        };
    }

    private static uint Clamp(double value, uint min, uint max)
    {
        return (uint)Math.Clamp(value, min, max);
    }

    /// <summary>
    /// 构造候选。材料来源：
    /// - 元素：7 种元素，选中后取该元素的晶石碎屑 ID（Positive 推导品质）
    /// - Boss / 周本：角色槽位反推（无法按类型区分），未引用的新角色培养素材归入 Boss
    /// - 地区特产：按 TypeDescription 全量列出
    /// - 天赋书 / 怪物 / 武器突破：按连续 ID 归系列，排除智识之冕 / 原质
    /// - 怪物掉落：角色用角色槽位反推的系列，武器用武器槽位反推的系列（茁壮菌核只在武器）
    /// </summary>
    private void InitializeChoices(CultivationMetadataContext context)
    {
        ElementChoices.Clear();
        BossChoices.Clear();
        SpecialtyChoices.Clear();
        TalentBookChoices.Clear();
        AvatarMonsterChoices.Clear();
        WeeklyBossChoices.Clear();
        WeaponAscensionChoices.Clear();
        WeaponMonster1Choices.Clear();
        WeaponMonster2Choices.Clear();
        QualityChoices.Clear();

        // 7 种元素，用已有的「名称 + 编号」有序表按编号排序，再用转换器映射回 ElementType。
        foreach (string name in Ellen.Launcher.Model.Intrinsic.Frozen.IntrinsicFrozen.ElementNameValues
            .OrderBy(static item => item.Value)
            .Select(static item => item.Name))
        {
            ElementChoices.Add(new(name, ElementNameIconConverter.ElementNameToElementType(name)));
        }

        // 地区特产：全量
        foreach (Material material in context.Materials
            .Where(static material => material.TypeDescription is { } description && description.Contains("区域特产"))
            .DistinctBy(static material => material.Id)
            .OrderBy(static material => material.Name, StringComparer.CurrentCulture))
        {
            SpecialtyChoices.Add(new(material));
        }

        // 角色槽位反推 Boss / 周本 / 怪物系列
        HashSet<MaterialId> bossIds = [];
        HashSet<MaterialId> weeklyBossIds = [];
        HashSet<MaterialId> roleMonsterIds = [];
        foreach (Model.Metadata.Avatar.Avatar avatar in context.IdAvatarMap.Values)
        {
            ImmutableArray<MaterialId> items = avatar.CultivationItems;
            if (items.Length > 1)
            {
                bossIds.Add(items[1]);
            }

            if (items.Length > 5)
            {
                weeklyBossIds.Add(items[5]);
            }

            if (items.Length > 3)
            {
                roleMonsterIds.Add(items[3]);
            }
        }

        // 未被任何角色引用的「角色培养素材」（如破碎的稳定锚）归入 Boss 下拉，确保新材料也出现
        HashSet<MaterialId> allRoleCultivation = context.Materials
            .Where(static material => material.TypeDescription == SH.ModelMetadataMaterialCharacterLevelUpMaterial)
            .Select(static material => material.Id)
            .ToHashSet();
        foreach (MaterialId id in allRoleCultivation.Except(bossIds).Except(weeklyBossIds))
        {
            bossIds.Add(id);
        }

        AddSingleChoices(BossChoices, context, bossIds);
        AddSingleChoices(WeeklyBossChoices, context, weeklyBossIds);

        // 全量构建「材料 ID → 完整品质系列」映射。角色/武器槽位只存某个品质的 ID，
        // 必须靠映射才能补全同一系列的其他品质（例如槽位存 112151 源生嵌合体，需补全 112149/112150）。
        Dictionary<uint, ImmutableArray<uint>> talentSeriesMap = BuildSeriesMap(
            context.Materials
                .Where(static material => material.TypeDescription == SH.ModelMetadataMaterialCharacterTalentMaterial && material.Id != CrownMaterialId)
                .Select(static material => (uint)material.Id),
            3);
        Dictionary<uint, ImmutableArray<uint>> monsterSeriesMap = BuildSeriesMap(
            context.Materials
                .Where(static material => material.TypeDescription == SH.ModelMetadataMaterialCharacterAndWeaponEnhancementMaterial && material.Id > ProtoMaterialId)
                .Select(static material => (uint)material.Id),
            3);
        Dictionary<uint, ImmutableArray<uint>> weaponAscensionSeriesMap = BuildSeriesMap(
            context.Materials
                .Where(static material => material.TypeDescription == SH.ModelMetadataMaterialWeaponAscensionMaterial)
                .Select(static material => (uint)material.Id),
            4);

        AddMappedSeries(TalentBookChoices, context, talentSeriesMap.Keys, talentSeriesMap);
        AddMappedSeries(AvatarMonsterChoices, context, roleMonsterIds.Select(static id => (uint)id), monsterSeriesMap);

        // 武器三类突破材料：武器槽位反推（武器本 4 连组，怪物 A / B 各 3 连组）
        HashSet<MaterialId> weaponAscensionIds = [];
        HashSet<MaterialId> weaponMonsterAIds = [];
        HashSet<MaterialId> weaponMonsterBIds = [];
        foreach (Model.Metadata.Weapon.Weapon weapon in context.IdWeaponMap.Values)
        {
            ImmutableArray<MaterialId> items = weapon.CultivationItems;
            if (items.Length > 0)
            {
                weaponAscensionIds.Add(items[0]);
            }

            if (items.Length > 1)
            {
                weaponMonsterAIds.Add(items[1]);
            }

            if (items.Length > 2)
            {
                weaponMonsterBIds.Add(items[2]);
            }
        }

        AddMappedSeries(WeaponAscensionChoices, context, weaponAscensionIds.Select(static id => (uint)id), weaponAscensionSeriesMap);
        AddMappedSeries(WeaponMonster1Choices, context, weaponMonsterAIds.Select(static id => (uint)id), monsterSeriesMap);
        AddMappedSeries(WeaponMonster2Choices, context, weaponMonsterBIds.Select(static id => (uint)id), monsterSeriesMap);

        QualityChoices.Add(new(SH.ModelIntrinsicItemQualityBlue, QualityType.QUALITY_BLUE));
        QualityChoices.Add(new(SH.ModelIntrinsicItemQualityPurple, QualityType.QUALITY_PURPLE));
        QualityChoices.Add(new(SH.ModelIntrinsicItemQualityOrange, QualityType.QUALITY_ORANGE));
    }

    /// <summary>全量材料按「连续 N 个一组」归成系列，并让系列内每个 ID 都能查回完整系列。</summary>
    private static Dictionary<uint, ImmutableArray<uint>> BuildSeriesMap(IEnumerable<uint> ids, int seriesSize)
    {
        Dictionary<uint, ImmutableArray<uint>> map = [];
        ImmutableArray<uint>.Builder current = ImmutableArray.CreateBuilder<uint>(seriesSize);

        foreach (uint id in ids.Order())
        {
            if (current.Count > 0 && (id != current[^1] + 1 || current.Count >= seriesSize))
            {
                RegisterSeries(map, current.ToImmutable());
                current = ImmutableArray.CreateBuilder<uint>(seriesSize);
            }

            current.Add(id);
        }

        if (current.Count > 0)
        {
            RegisterSeries(map, current.ToImmutable());
        }

        return map;
    }

    private static void RegisterSeries(Dictionary<uint, ImmutableArray<uint>> map, ImmutableArray<uint> series)
    {
        foreach (uint id in series)
        {
            map[id] = series;
        }
    }

    /// <summary>用一组材料 ID（来自槽位或全量）查映射，补全成完整系列后生成候选项，同一系列只添加一个。</summary>
    private static void AddMappedSeries(
        ObservableCollection<SeriesChoice> target,
        CultivationMetadataContext context,
        IEnumerable<uint> ids,
        Dictionary<uint, ImmutableArray<uint>> seriesMap)
    {
        HashSet<uint> added = [];
        foreach (uint id in ids.Order())
        {
            if (!seriesMap.TryGetValue(id, out ImmutableArray<uint> series) || !added.Add(series[0]))
            {
                continue;
            }

            ImmutableArray<MaterialChoice>.Builder materials = ImmutableArray.CreateBuilder<MaterialChoice>(series.Length);
            foreach (uint seriesId in series)
            {
                if (context.IdMaterialMap.TryGetValue((MaterialId)seriesId, out Material? material))
                {
                    materials.Add(new(material));
                }
            }

            if (materials.Count > 0)
            {
                target.Add(new(materials.ToImmutable()));
            }
        }
    }

    private static void AddSingleChoices(
        ObservableCollection<MaterialChoice> target,
        CultivationMetadataContext context,
        IEnumerable<MaterialId> ids)
    {
        foreach (MaterialId id in ids.Order())
        {
            if ((uint)id is ProtoMaterialId or CrownMaterialId)
            {
                continue;
            }

            if (context.IdMaterialMap.TryGetValue(id, out Material? material))
            {
                target.Add(new(material));
            }
        }
    }
}

/// <summary>7 种元素之一，选中后取该元素的晶石碎屑 ID。</summary>
public sealed class ElementChoice(string name, ElementType element)
{
    public string Name { get; } = name;

    public ElementType Element { get; } = element;
}

/// <summary>单个材料候选项（特产 / Boss / 周本）。</summary>
public sealed class MaterialChoice
{
    public MaterialChoice(Material material)
    {
        Id = material.Id;
        Name = material.Name;
        Icon = material.Icon;
        Quality = material.RankLevel;
    }

    public uint Id { get; }

    public string Name { get; }

    public string Icon { get; }

    public QualityType Quality { get; }
}

/// <summary>一个品质系列候选项，包含从低到高的全部品质材料，显示时图标 + 名称串联。</summary>
public sealed class SeriesChoice
{
    public SeriesChoice(ImmutableArray<MaterialChoice> materials)
    {
        Materials = materials;
        Ids = [.. materials.Select(static material => material.Id)];
    }

    public ImmutableArray<MaterialChoice> Materials { get; }

    public ImmutableArray<uint> Ids { get; }
}

public sealed class QualityChoice
{
    public QualityChoice(string name, QualityType quality)
    {
        Name = name;
        Quality = quality;
    }

    public string Name { get; set; }

    public QualityType Quality { get; set; }
}

/// <summary>
/// 弹窗结果：角色模板与武器模板各自独立，保存时由 ViewModel 调用离线计算器生成材料并新建条目。
/// </summary>
public sealed record CustomMaterialPlanResult(
    AvatarPromotionDelta? AvatarDelta,
    CustomAvatarMaterialSelection? AvatarSelection,
    AvatarPromotionDelta? WeaponDelta,
    CustomWeaponMaterialSelection? WeaponSelection);
