// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;

namespace Ellen.Launcher.Service.DailyNote.NotifySuppression;

public interface INotifySuppressionContext
{
    DailyNoteEntry Entry { get; }

    Web.Hoyolab.Takumi.GameRecord.DailyNote.DailyNote DailyNote
    {
        get
        {
            ArgumentNullException.ThrowIfNull(Entry.DailyNote);
            return Entry.DailyNote;
        }
    }

    void Add(DailyNoteNotifyInfo info);
}