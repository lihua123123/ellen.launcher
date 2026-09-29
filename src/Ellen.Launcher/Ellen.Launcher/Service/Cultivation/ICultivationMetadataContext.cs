// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;

namespace Ellen.Launcher.Service.Cultivation;

public interface ICultivationMetadataContext : IMetadataContext,
    IMetadataArrayGachaEventSource,
    IMetadataArrayMaterialSource,
    IMetadataDictionaryIdMaterialSource,
    IMetadataDictionaryIdAvatarSource,
    IMetadataDictionaryIdWeaponSource,
    IMetadataDictionaryResultMaterialIdCombineSource
{
    Item GetAvatarItem(AvatarId avatarId);

    Item GetWeaponItem(WeaponId weaponId);
}