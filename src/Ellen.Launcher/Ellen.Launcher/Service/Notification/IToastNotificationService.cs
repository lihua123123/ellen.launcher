// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Notification;

public interface IToastNotificationService
{
    void Show(string rawXml, bool suppressDisplay = false);

    void ShowText(string text);
}
