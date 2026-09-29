// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;

public interface IMetadataDictionaryIdWeaponSource
{
    ImmutableDictionary<WeaponId, Model.Metadata.Weapon.Weapon> IdWeaponMap { get; set; }
}