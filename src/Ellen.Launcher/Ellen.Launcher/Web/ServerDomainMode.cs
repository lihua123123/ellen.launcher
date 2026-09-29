// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web;

[ExtendedEnum]
public enum ServerDomainMode
{
    [LocalizationKey(nameof(SH.WebServerDomainModePrimary))]
    Primary,

    [LocalizationKey(nameof(SH.WebServerDomainModeBackup))]
    Backup,
}
