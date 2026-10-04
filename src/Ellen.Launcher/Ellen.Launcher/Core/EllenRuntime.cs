// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Microsoft.Windows.AppNotifications;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Core.IO.Hashing;
using Ellen.Launcher.Core.Setting;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Windows.ApplicationModel;
using Windows.Storage;

namespace Ellen.Launcher.Core;

public static class EllenRuntime
{
    public static Version Version { get; } = RuntimeEnvironment.IsPackaged ? Package.Current.Id.Version.ToVersion() : AppVersion.CurrentVersion;

    public static string UserAgent { get; } = $"Ellen/{Version}";

    public static string DataDirectory { get; } = InitializeDataDirectory();

    public static string LocalCacheDirectory { get; } = RuntimeEnvironment.IsPackaged
        ? ApplicationData.Current.LocalCacheFolder.Path
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"EllenLauncher\Cache");

    public static string FamilyName { get; } = RuntimeEnvironment.IsPackaged
        ? Package.Current.Id.FamilyName
        : "Ellen.Launcher";

    public static string DeviceId { get; } = InitializeDeviceId();

    public static WebView2Version WebView2Version { get; } = InitializeWebView2();

    public static bool IsProcessElevated { get; } = LocalSetting.Get(SettingKeys.OverrideElevationRequirement, false) || Environment.IsPrivilegedProcess;

    // Requires main thread
    public static bool IsAppNotificationEnabled { get; } = AppNotificationManager.Default.Setting is AppNotificationSetting.Enabled or AppNotificationSetting.Unsupported;

    public static string? GetDisplayName()
    {
        // AppNameAndVersion
        // AppDevNameAndVersion
        // AppElevatedNameAndVersion
        // AppElevatedDevNameAndVersion
        string name = new StringBuilder()
            .Append("App")
            .AppendIf(IsProcessElevated, "Elevated")
#if DEBUG
            .Append("Dev")
#endif
            .Append("NameAndVersion")
            .ToString();

        Debug.Assert(XamlApplicationLifetime.CultureInfoInitialized);
        string? displayName = SH.GetString(name, Version);
        return displayName is null ? null : string.Intern(displayName);
    }

    public static string? GetDisplayNameForNotifyIcon()
    {
        // AppName
        // AppDevName
        string name = new StringBuilder()
            .Append("App")
#if DEBUG
            .Append("Dev")
#endif
            .Append("Name")
            .ToString();

        Debug.Assert(XamlApplicationLifetime.CultureInfoInitialized);
        string? displayName = SH.GetString(name);
        return displayName is null ? null : string.Intern(displayName);
    }

    public static ValueFile GetDataDirectoryFile(string fileName)
    {
        return string.Intern(Path.Combine(DataDirectory, fileName));
    }

    public static ValueFile GetDataUpdateCacheDirectoryFile(string fileName)
    {
        string directory = Path.Combine(DataDirectory, "UpdateCache");
        Directory.CreateDirectory(directory);
        return string.Intern(Path.Combine(directory, fileName));
    }

    public static ValueDirectory GetDataServerCacheDirectory()
    {
        string directory = Path.Combine(DataDirectory, "ServerCache");
        Directory.CreateDirectory(directory);
        return string.Intern(directory);
    }

    /// <summary>
    /// ellen.wiki 数据快照的本地缓存目录（运行期同步写入）。
    /// </summary>
    public static ValueDirectory GetDataEllenWikiDirectory()
    {
        string directory = Path.Combine(DataDirectory, "EllenWiki");
        Directory.CreateDirectory(directory);
        return string.Intern(directory);
    }

    public static ValueDirectory GetDataBackgroundDirectory()
    {
        string directory = Path.Combine(DataDirectory, "Background");
        Directory.CreateDirectory(directory);
        return string.Intern(directory);
    }

    public static ValueDirectory GetDataScreenshotDirectory()
    {
        string directory = Path.Combine(DataDirectory, "Screenshot");
        Directory.CreateDirectory(directory);
        return string.Intern(directory);
    }

    public static ValueDirectory GetDataRepositoryDirectory()
    {
        string directory = Path.Combine(DataDirectory, "Repository");
        Directory.CreateDirectory(directory);
        return string.Intern(directory);
    }

    public static ValueDirectory GetLocalCacheImageCacheDirectory()
    {
        string directory = Path.Combine(LocalCacheDirectory, "ImageCache");
        Directory.CreateDirectory(directory);
        return string.Intern(directory);
    }

    public static string GetWebView2UserDataDirectory()
    {
        string directory = Path.Combine(DataDirectory, "WebView2");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string InitializeDataDirectory()
    {
        // Delete the previous data folder if it exists
        try
        {
            string previousDirectory = LocalSetting.Get(SettingKeys.PreviousDataDirectoryToDelete, string.Empty);
            if (!string.IsNullOrEmpty(previousDirectory) && Directory.Exists(previousDirectory))
            {
                Directory.SetReadOnly(previousDirectory, false);
                Directory.Delete(previousDirectory, true);
            }
        }
        finally
        {
            LocalSetting.Set(SettingKeys.PreviousDataDirectoryToDelete, string.Empty);
        }

        // Check if the preferred path is set
        string currentDirectory = LocalSetting.Get(SettingKeys.DataDirectory, string.Empty);

        if (!string.IsNullOrEmpty(currentDirectory))
        {
            Directory.CreateDirectory(currentDirectory);
            return currentDirectory;
        }

        const string FolderName
#if IS_ALPHA_BUILD
        = "EllenAlpha";
#elif IS_CANARY_BUILD
        = "EllenCanary";
#else
        = "Ellen";
#endif

        // Check if the old documents path exists
        string myDocumentsEllenDirectory = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), FolderName));
        if (Directory.Exists(myDocumentsEllenDirectory))
        {
            LocalSetting.Set(SettingKeys.DataDirectory, myDocumentsEllenDirectory);
            return myDocumentsEllenDirectory;
        }

        // Prefer LocalApplicationData
        string localApplicationData = RuntimeEnvironment.IsPackaged
            ? ApplicationData.Current.LocalFolder.Path
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        string finalFolderName = RuntimeEnvironment.IsPackaged ? FolderName : "EllenLauncher";
        string path = Path.GetFullPath(Path.Combine(localApplicationData, finalFolderName));
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception ex)
        {
            // FileNotFoundException | UnauthorizedAccessException
            // We don't have enough permission
            EllenException.InvalidOperation($"Failed to create data folder: {path}", ex);
        }

        LocalSetting.Set(SettingKeys.DataDirectory, path);
        return path;
    }

    private static string InitializeDeviceId()
    {
        string userName = Environment.UserName;
        object? machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography\", "MachineGuid", userName);
        return Hash.ToHexString(HashAlgorithmName.MD5, $"{userName}{machineGuid}");
    }

    private static WebView2Version InitializeWebView2()
    {
        try
        {
            string version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return new(version, version, true);
        }
        catch (FileNotFoundException)
        {
            return new(string.Empty, SH.CoreWebView2HelperVersionUndetected, false);
        }
    }
}