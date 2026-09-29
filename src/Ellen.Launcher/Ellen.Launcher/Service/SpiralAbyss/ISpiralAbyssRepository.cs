// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Frozen;

namespace Ellen.Launcher.Service.SpiralAbyss;

public interface ISpiralAbyssRepository : IRepository<SpiralAbyssEntry>
{
    void AddSpiralAbyssEntry(SpiralAbyssEntry entry);

    FrozenDictionary<uint, SpiralAbyssEntry> GetSpiralAbyssEntryMapByUid(string uid);

    void UpdateSpiralAbyssEntry(SpiralAbyssEntry entry);
}