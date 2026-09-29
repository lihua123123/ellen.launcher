// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.Calendar;

public sealed class CalendarMetadataContext : CultivationMetadataContext, IMetadataContext,
    ICultivationMetadataContext,
    IMetadataArrayAvatarSource,
    IMetadataArrayWeaponSource,
    IMetadataDictionaryIdMaterialSource
{
    public ImmutableArray<Avatar> Avatars { get; set; }

    public ImmutableArray<Weapon> Weapons { get; set; }
}