// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Reliquary;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Service.Backpack;

namespace Ellen.Launcher.ViewModel.Backpack;

public class BackpackItemView
{
    public BackpackItem Entity { get; protected init; } = default!;

    public BackpackItemCategory Category { get; protected set; }

    public Material? Material { get; protected set; }

    public string Name { get; protected set; } = string.Empty;

    public string DisplayCount => Entity.Count > 1 ? $"x{Entity.Count}" : string.Empty;

    public string Description { get; protected set; } = string.Empty;

    public string TypeDescription { get; protected set; } = string.Empty;

    public Uri IconUri { get; protected set; } = default!;

    public QualityType Quality { get; protected set; }

    public ImmutableArray<string> SetDescriptions { get; protected set; } = [];

    public bool HasSetDescriptions => !SetDescriptions.IsDefaultOrEmpty;

    public static BackpackItemView Create(BackpackItem entity, BackpackServiceMetadataContext context)
    {
        if (context.IdWeaponMap.TryGetValue(entity.ItemId, out Weapon? weapon))
        {
            return BackpackWeaponItemView.Create(entity, context, weapon);
        }

        if (entity.MainPropId is not null && context.IdReliquaryMap.TryGetValue(entity.ItemId, out Reliquary? reliquary))
        {
            return BackpackReliquaryItemView.Create(entity, context, reliquary);
        }

        // First try to classify by ItemId alone
        // If that returns Material (default), try to get Material metadata and classify by MaterialType
        context.IdMaterialMap.TryGetValue(entity.ItemId, out Material? material);

        BackpackItemView view = new()
        {
            Entity = entity,
            Material = material,
            Category = material.GetCategory(entity.ItemId),
            // IdMaterialMap 缺元数据时 Name/Icon 都是空的，兵底进 Material 桶后至少要用可辨认的文本
            // 占位（否则是一片空白卡）。真正的修复是补齐 IdMaterialMap。
            Name = material?.Name ?? $"#{entity.ItemId}",
            Description = material?.Description ?? string.Empty,
            TypeDescription = material?.TypeDescription ?? string.Empty,
            IconUri = material is not null ? ItemIconConverter.IconNameToUri(material.Icon) : default!,
            Quality = material?.RankLevel ?? QualityType.QUALITY_NONE,
        };

        return view;
    }
}
