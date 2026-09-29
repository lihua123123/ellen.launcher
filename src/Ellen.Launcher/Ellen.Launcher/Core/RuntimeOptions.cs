// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core;

[Service(ServiceLifetime.Singleton)]
public sealed class RuntimeOptions
{
    [Obsolete("This property only exist for binding purpose")]
    public Version Version { get => EllenRuntime.Version; }

    [Obsolete("This property only exist for binding purpose")]
    public string DataFolder { get => EllenRuntime.DataDirectory; }

    [Obsolete("This property only exist for binding purpose")]
    public string DeviceId { get => EllenRuntime.DeviceId; }

    [Obsolete("This property only exist for binding purpose")]
    public string WebView2Version { get => EllenRuntime.WebView2Version.Version; }

    [Obsolete("This property only exist for binding purpose")]
    public bool IsToastAvailable { get => EllenRuntime.IsAppNotificationEnabled; }
}