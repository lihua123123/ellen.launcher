// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Calculable;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.ViewModel.GachaLog;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata.Weapon;

public sealed partial class Weapon : IDefaultIdentity<WeaponId>,
    INameQualityAccess,
    IStatisticsItemConvertible,
    ISummaryItemConvertible,
    IItemConvertible,
    ICalculableSource<ICalculableWeapon>,
    ICultivationItemsAccess,
    IPropertyValuesProvider
{
    public required WeaponId Id { get; init; }

    public required PromoteId PromoteId { get; init; }

    public required uint Sort { get; init; }

    public required WeaponType WeaponType { get; init; }

    public required QualityType RankLevel { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Icon { get; init; }

    public required string AwakenIcon { get; init; }

    public required WeaponTypeValueCollection GrowCurves { get; init; }

    public NameDescriptions? Affix { get; init; }

    public required ImmutableArray<MaterialId> CultivationItems { get; init; }

    [JsonIgnore]
    public List<Material>? CultivationItemsView { get; set; }

    [JsonIgnore]
    public QualityType Quality
    {
        get => RankLevel;
    }

    public uint MaxLevel { get => GetMaxLevelByQuality(Quality); }

    public static uint GetMaxLevelByQuality(QualityType quality)
    {
        return quality >= QualityType.QUALITY_BLUE ? 90U : 70U;
    }

    public ICalculableWeapon ToCalculable()
    {
        return CalculableWeapon.From(this);
    }

    public TItem ToItem<TItem>()
        where TItem : Model.Item, new()
    {
        return new()
        {
            Id = Id,
            Name = Name,
            Icon = EquipIconConverter.IconNameToUri(Icon),
            Badge = WeaponTypeIconConverter.WeaponTypeToIconUri(WeaponType),
            Quality = RankLevel,
        };
    }

    public StatisticsItem ToStatisticsItem(int count)
    {
        return new()
        {
            Id = Id,
            Name = Name,
            Icon = EquipIconConverter.IconNameToUri(Icon),
            Badge = WeaponTypeIconConverter.WeaponTypeToIconUri(WeaponType),
            Quality = RankLevel,
            Count = count,
        };
    }

    public SummaryItem ToSummaryItem(int lastPull, in DateTimeOffset time, bool isUp)
    {
        return new()
        {
            Id = Id,
            Name = Name,
            Icon = EquipIconConverter.IconNameToUri(Icon),
            Badge = WeaponTypeIconConverter.WeaponTypeToIconUri(WeaponType),
            Time = time,
            Quality = RankLevel,
            LastPull = lastPull,
            IsUp = isUp,
        };
    }
}