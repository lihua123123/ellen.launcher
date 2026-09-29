// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.RoleCombat;

public sealed class RoleCombatMetadataContext : IMetadataContext,
    IMetadataDictionaryIdRoleCombatScheduleSource,
    IMetadataDictionaryIdAvatarWithPlayersSource
{
    public ImmutableDictionary<RoleCombatScheduleId, RoleCombatSchedule> IdRoleCombatScheduleMap { get; set; } = default!;

    public ImmutableDictionary<AvatarId, Avatar> IdAvatarMap { get; set; } = default!;
}