// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.ViewModel.User;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.DailyNote;

public interface IDailyNoteService
{
    ValueTask AddDailyNoteAsync(DailyNoteMetadataContext context, UserAndUid userAndUid, CancellationToken token = default);

    ValueTask<ObservableCollection<DailyNoteEntry>> GetDailyNoteEntryCollectionAsync(DailyNoteMetadataContext context, bool forceRefresh = false, CancellationToken token = default);

    ValueTask RefreshDailyNotesAsync(CancellationToken token = default);

    ValueTask RemoveDailyNoteAsync(DailyNoteEntry entry, CancellationToken token = default);

    ValueTask<bool> UpdateDailyNoteAsync(DailyNoteEntry entry, CancellationToken token = default);
}