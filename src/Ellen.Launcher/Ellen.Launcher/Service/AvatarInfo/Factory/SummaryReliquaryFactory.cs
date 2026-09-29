// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Service.AvatarInfo.Factory.Builder;
using Ellen.Launcher.ViewModel.AvatarProperty;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;
using DetailedReliquary = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar.Reliquary;
using MetadataReliquary = Ellen.Launcher.Model.Metadata.Reliquary.Reliquary;
using MetadataReliquarySet = Ellen.Launcher.Model.Metadata.Reliquary.ReliquarySet;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

public sealed class SummaryReliquaryFactory
{
    private readonly SummaryFactoryMetadataContext metadataContext;
    private readonly DetailedReliquary reliquary;
    private readonly double baseHp;
    private readonly double baseAttack;
    private readonly double baseDefense;

    public SummaryReliquaryFactory(
        SummaryFactoryMetadataContext metadataContext,
        DetailedReliquary reliquary,
        double baseHp,
        double baseAttack,
        double baseDefense)
    {
        this.metadataContext = metadataContext;
        this.reliquary = reliquary;
        this.baseHp = baseHp;
        this.baseAttack = baseAttack;
        this.baseDefense = baseDefense;
    }

    public static ReliquaryView Create(
        SummaryFactoryMetadataContext metadataContext,
        DetailedReliquary reliquary,
        double baseHp,
        double baseAttack,
        double baseDefense)
    {
        return new SummaryReliquaryFactory(metadataContext, reliquary, baseHp, baseAttack, baseDefense).Create();
    }

    public ReliquaryView Create()
    {
        MetadataReliquary metaReliquary = metadataContext.IdReliquaryMap[reliquary.Id];
        MetadataReliquarySet metaReliquarySet = metadataContext.IdReliquarySetMap[metaReliquary.SetId];

        ReliquaryViewBuilder reliquaryViewBuilder = new ReliquaryViewBuilder()
            .SetName(metaReliquary.Name)
            .SetIcon(RelicIconConverter.IconNameToUri(metaReliquary.Icon))
            .SetDescription(metaReliquary.Description)
            .SetLevel($"+{reliquary.Level}")
            .SetQuality(metaReliquary.RankLevel)
            .SetEquipType(metaReliquary.EquipType)
            .SetSetName(metaReliquarySet.Name)
            .SetMainProperty(FightPropertyFormat.ToNameValue(reliquary.MainProperty))
            .SetComposedSubProperties(reliquary.SubPropertyList.SelectAsArray(CreateSubProperty));

        return reliquaryViewBuilder.View;
    }

    private ReliquaryComposedSubProperty CreateSubProperty(ReliquaryProperty property)
    {
        double enhancementCount = ReliquaryEnhancementCalculator.Calculate(
            property.PropertyType,
            property.Value,
            baseHp,
            baseAttack,
            baseDefense);

        return new(property.PropertyType, property.Value, property.Times + 1, enhancementCount);
    }
}
