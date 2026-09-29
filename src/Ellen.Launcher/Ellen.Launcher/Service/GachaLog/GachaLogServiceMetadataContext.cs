// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.GachaLog;

public sealed class GachaLogServiceMetadataContext : IMetadataContext,
    IMetadataArrayGachaEventSource,
    IMetadataDictionaryIdAvatarSource,
    IMetadataDictionaryIdWeaponSource,
    IMetadataDictionaryNameAvatarSource,
    IMetadataDictionaryNameWeaponSource,
    IMetadataDictionaryIdBeyondItemSource
{
    public ImmutableArray<GachaEvent> GachaEvents { get; set; } = default!;

    public ImmutableDictionary<AvatarId, Avatar> IdAvatarMap { get; set; } = default!;

    public ImmutableDictionary<WeaponId, Weapon> IdWeaponMap { get; set; } = default!;

    public ImmutableDictionary<string, Avatar> NameAvatarMap { get; set; } = default!;

    public ImmutableDictionary<string, Weapon> NameWeaponMap { get; set; } = default!;
    public ImmutableDictionary<BeyondItemId, BeyondItem> IdBeyondItemMap { get; set; } = default!;

    public Item GetItemByNameAndType(string name, string type)
    {
        if (string.Equals(type, SH.ModelInterchangeUIGFItemTypeAvatar, StringComparison.Ordinal))
        {
            return this.GetAvatar(name).GetOrCreateItem();
        }

        if (string.Equals(type, SH.ModelInterchangeUIGFItemTypeWeapon, StringComparison.Ordinal))
        {
            return this.GetWeapon(name).GetOrCreateItem();
        }

        throw EllenException.NotSupported($"Unsupported item type and name: '{type},{name}'");
    }

    public uint GetItemId(GachaLogItem item)
    {
        if (string.Equals(item.ItemType, SH.ModelInterchangeUIGFItemTypeAvatar, StringComparison.Ordinal))
        {
            return this.GetAvatar(item.Name).Id;
        }

        if (string.Equals(item.ItemType, SH.ModelInterchangeUIGFItemTypeWeapon, StringComparison.Ordinal))
        {
            return this.GetWeapon(item.Name).Id;
        }

        throw EllenException.NotSupported($"Unsupported item type and name: '{item.ItemType},{item.Name}'");
    }

    public INameQualityAccess GetNameQualityByItemId(uint id)
    {
        uint place = id.StringLength;
        return place switch
        {
            8U => IdAvatarMap[id],
            5U => IdWeaponMap[id],
            _ => throw EllenException.NotSupported($"Id places: {place}"),
        };
    }
}