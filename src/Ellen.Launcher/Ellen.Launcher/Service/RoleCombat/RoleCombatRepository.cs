// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Frozen;

namespace Ellen.Launcher.Service.RoleCombat;

[Service(ServiceLifetime.Singleton, typeof(IRoleCombatRepository))]
public sealed partial class RoleCombatRepository : IRoleCombatRepository
{
    [GeneratedConstructor]
    public partial RoleCombatRepository(IServiceProvider serviceProvider);

    public partial IServiceProvider ServiceProvider { get; }

    public FrozenDictionary<uint, RoleCombatEntry> GetRoleCombatEntryMapByUid(string uid)
    {
        return this.Query(query => query
            .Where(s => s.Uid == uid)
            .OrderByDescending(s => s.ScheduleId)
            .ToFrozenDictionary(e => e.ScheduleId));
    }

    public void UpdateRoleCombatEntry(RoleCombatEntry entry)
    {
        this.Update(entry);
    }

    public void AddRoleCombatEntry(RoleCombatEntry entry)
    {
        this.Add(entry);
    }
}