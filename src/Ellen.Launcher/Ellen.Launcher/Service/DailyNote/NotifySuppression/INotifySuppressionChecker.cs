// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.DailyNote.NotifySuppression;

public interface INotifySuppressionChecker
{
    bool ShouldNotify(INotifySuppressionContext context);

    bool GetIsSuppressed(INotifySuppressionContext context);

    void SetIsSuppressed(INotifySuppressionContext context, bool suppressed);

    DailyNoteNotifyInfo NotifyInfo(INotifySuppressionContext context);
}