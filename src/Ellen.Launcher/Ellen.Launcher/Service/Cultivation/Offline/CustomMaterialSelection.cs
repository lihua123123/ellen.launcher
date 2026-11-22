// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Cultivation.Offline;

/// <summary>
/// 自定义材料计划里角色侧选中的材料。数量由等级模板算出；这里只保存"是哪几种材料"。
///
/// 有品质分级的材料（天赋书 / 怪物掉落）保存整个系列从低到高的全部 ID，
/// 计算时按偏移下标直接取值，避免用「最高品质 ID - 偏移」推导时越界误命中相邻系列
/// （例如 104303 - 3 会命中 104300 无主的命星）。
/// </summary>
public sealed class CustomAvatarMaterialSelection
{
    /// <summary>元素晶石最低品质（碎屑）ID； Positive 用 +偏移推导高品质。</summary>
    public uint GemBaseId { get; set; }

    public uint BossMaterialId { get; set; }

    public uint SpecialtyId { get; set; }

    /// <summary>天赋书系列从低到高的 ID（教导 / 指引 / 哲学）。</summary>
    public ImmutableArray<uint> TalentBookIds { get; set; } = [];

    /// <summary>怪物掉落系列从低到高的 ID。</summary>
    public ImmutableArray<uint> MonsterMaterialIds { get; set; } = [];

    public uint WeeklyBossMaterialId { get; set; }

    public bool IsConfigured
    {
        get => GemBaseId is not 0 || TalentBookIds.Length > 0 || MonsterMaterialIds.Length > 0
            || SpecialtyId is not 0 || BossMaterialId is not 0 || WeeklyBossMaterialId is not 0;
    }

    /// <summary>按品质偏移从系列里取 ID；偏移是从最高品质往回数的距离。</summary>
    public static uint GetIdByQualityOffset(ImmutableArray<uint> ids, uint offsetFromHighest)
    {
        if (ids.IsEmpty)
        {
            return 0U;
        }

        int index = ids.Length - 1 - (int)offsetFromHighest;
        return index >= 0 && index < ids.Length ? ids[index] : 0U;
    }
}

/// <summary>
/// 自定义材料计划里武器侧选中的材料。三个突破材料槽各自保存完整品质系列（从低到高），
/// 摩拉 / 精锻用魔矿按模板算，不在此配置。
/// </summary>
public sealed class CustomWeaponMaterialSelection
{
    /// <summary>三个突破材料槽，每个是同一系列从低到高的 ID 列表。</summary>
    public ImmutableArray<ImmutableArray<uint>> AscensionMaterialSeries { get; set; } = [];

    public QualityType Quality { get; set; } = QualityType.QUALITY_ORANGE;

    public bool IsConfigured
    {
        get => AscensionMaterialSeries.Any(static series => !series.IsEmpty);
    }

    public static uint GetIdByQualityOffset(ImmutableArray<uint> series, uint offsetFromHighest)
    {
        return CustomAvatarMaterialSelection.GetIdByQualityOffset(series, offsetFromHighest);
    }
}
