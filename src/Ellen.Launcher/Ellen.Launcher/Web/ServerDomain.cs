// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.


namespace Ellen.Launcher.Web;

public static class ServerDomain
{
    private static volatile ServerDomainMode currentMode = ServerDomainMode.Primary;

    /// <summary>
    /// Sync mode from external setting (e.g. called after AppOptions loads persisted value).
    /// </summary>
    public static void SetMode(ServerDomainMode mode)
    {
        currentMode = mode;
    }

    public static string GetApiRoot()
    {
        return IsBackup() ? "https://api.hutaorp.org" : "https://api.snaphutaorp.org";
    }

    public static string GetRootDomain()
    {
        return IsBackup() ? "https://hutaorp.org" : "https://snaphutaorp.org";
    }

    /// <summary>
    /// Auto fallback: switch to backup domain in-memory only, without persisting the setting.
    /// </summary>
    public static void TryAutoFallback()
    {
        if (currentMode is ServerDomainMode.Primary)
        {
            currentMode = ServerDomainMode.Backup;
        }
    }

    public static bool IsBackupMode()
    {
        return currentMode is ServerDomainMode.Backup;
    }

    private static bool IsBackup()
    {
        return IsBackupMode();
    }
}
