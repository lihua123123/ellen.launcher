// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;

namespace Ellen.Launcher.Service.DailyNote.NotifySuppression;

public sealed class NotifySuppressionContext : INotifySuppressionContext
{
    private readonly List<DailyNoteNotifyInfo> infos;

    public NotifySuppressionContext(DailyNoteEntry entry, List<DailyNoteNotifyInfo> infos)
    {
        Entry = entry;
        this.infos = infos;
    }

    public DailyNoteEntry Entry { get; }

    public void Add(DailyNoteNotifyInfo info)
    {
        infos.Add(info);
    }
}