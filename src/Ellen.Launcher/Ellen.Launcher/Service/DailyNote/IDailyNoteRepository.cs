// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.DailyNote;

public interface IDailyNoteRepository : IRepository<DailyNoteEntry>
{
    void AddDailyNoteEntry(DailyNoteEntry entry);

    bool ContainsUid(string uid);

    void DeleteDailyNoteEntryById(Guid entryId);

    ImmutableArray<DailyNoteEntry> GetDailyNoteEntryImmutableArrayIncludingUser();

    void UpdateDailyNoteEntry(DailyNoteEntry entry);
}