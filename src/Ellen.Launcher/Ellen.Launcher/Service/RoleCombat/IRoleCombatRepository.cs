// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Frozen;

namespace Ellen.Launcher.Service.RoleCombat;

public interface IRoleCombatRepository : IRepository<RoleCombatEntry>
{
    void AddRoleCombatEntry(RoleCombatEntry entry);

    FrozenDictionary<uint, RoleCombatEntry> GetRoleCombatEntryMapByUid(string uid);

    void UpdateRoleCombatEntry(RoleCombatEntry entry);
}