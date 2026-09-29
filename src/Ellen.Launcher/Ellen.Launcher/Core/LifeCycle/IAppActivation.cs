// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Windows.AppNotifications;

namespace Ellen.Launcher.Core.LifeCycle;

public interface IAppActivation
{
    void RedirectedActivate(EllenActivationArguments args);

    void NotificationInvoked(AppNotificationManager manager, AppNotificationActivatedEventArgs args);

    void ActivateAndInitialize(EllenActivationArguments args);
}