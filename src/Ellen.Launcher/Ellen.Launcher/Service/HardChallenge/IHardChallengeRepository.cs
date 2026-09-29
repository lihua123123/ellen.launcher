// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Frozen;

namespace Ellen.Launcher.Service.HardChallenge;

public interface IHardChallengeRepository : IRepository<HardChallengeEntry>
{
    void AddHardChallengeEntry(HardChallengeEntry entry);

    FrozenDictionary<uint, HardChallengeEntry> GetHardChallengeMapByUid(string uid);

    void UpdateHardChallengeEntry(HardChallengeEntry entry);
}