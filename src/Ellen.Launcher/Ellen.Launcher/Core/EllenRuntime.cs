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

    // 缓存始终是数据目录下的 Cache 子目录：默认布局下与原来的
    // %LOCALAPPDATA%\EllenLauncher\Cache 完全一致，便携布局下自动跟到安装目录。
    public static string LocalCacheDirectory { get; } = RuntimeEnvironment.IsPackaged
        ? ApplicationData.Current.LocalCacheFolder.Path
        : Path.Combine(DataDirectory, "Cache");

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
        // 便携布局优先：安装器已经把旧数据复制到 {app}\Data，这里直接认它。
        // 不执行下面的「删旧目录」逻辑 —— 旧目录是复制来源，保留作为备份，
        // 而且误删用户 %LOCALAPPDATA% 内容的风险远大于省下的那点磁盘。
        if (!RuntimeEnvironment.IsPackaged && PortableLayout.DataDirectory is { } portableDirectory)
        {
            Directory.CreateDirectory(portableDirectory);
            LocalSetting.Set(SettingKeys.DataDirectory, portableDirectory);
            return portableDirectory;
        }

        // 用户在欢迎页/设置页选好了新目录：此刻数据库还没打开，可以安全地整体搬运。
        // 之前是在运行期复制，那时 Userdata.db 已被 SQLite 打开，复制出来的是空壳。
        ApplyPendingDataDirectory();

        // Delete the previous data folder if it exists
        try
        {
            string previousDirectory = LocalSetting.Get(SettingKeys.PreviousDataDirectoryToDelete, string.Empty);
            if (!string.IsNullOrEmpty(previousDirectory) && Directory.Exists(previousDirectory))
            {
                /* 默认布局下 settings.json 就放在这个目录里，删了它欢迎页会从头再来一遍，
                   而用户看到的就是「一直重启、打不开」。宁可留着占点空间。 */
                string settingsStore = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EllenLauncher"));

                if (string.Equals(Path.GetFullPath(previousDirectory), settingsStore, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.WriteLine($"跳过删除配置目录: {previousDirectory}");
                }
                else
                {
                    Directory.SetReadOnly(previousDirectory, false);
                    Directory.Delete(previousDirectory, true);
                }
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

    /// <summary>
    /// 执行「切换数据目录」的搬运阶段。
    /// 欢迎页/设置页只登记目标路径（<see cref="SettingKeys.PendingDataDirectory"/>），
    /// 真正的复制放到下一次启动、数据库尚未打开时进行：
    /// 运行期复制 Userdata.db 会得到空壳，用户会以为祈愿记录被清空了。
    /// </summary>
    private static void ApplyPendingDataDirectory()
    {
        string pending = LocalSetting.Get(SettingKeys.PendingDataDirectory, string.Empty);
        if (string.IsNullOrWhiteSpace(pending))
        {
            return;
        }

        try
        {
            string destination = Path.GetFullPath(pending);
            string source = LocalSetting.Get(SettingKeys.DataDirectory, string.Empty);

            if (string.IsNullOrWhiteSpace(source))
            {
                source = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EllenLauncher");
            }

            source = Path.GetFullPath(source);

            bool sameOrNested = string.Equals(source, destination, StringComparison.OrdinalIgnoreCase)
                || UrlPath.IsEqualOrSubdirectory(source, destination)
                || UrlPath.IsEqualOrSubdirectory(destination, source);

            if (!sameOrNested && Directory.Exists(source))
            {
                CopyDirectory(source, destination);
            }

            Directory.CreateDirectory(destination);
            LocalSetting.Set(SettingKeys.DataDirectory, destination);
        }
        catch (Exception ex)
        {
            /* 搬不动就留在原目录，绝不能因为换目录把程序卡在启动阶段 */
            Debug.WriteLine($"迁移数据目录失败，继续使用原目录: {ex}");
        }
        finally
        {
            LocalSetting.Set(SettingKeys.PendingDataDirectory, string.Empty);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            /* settings.json 由 LocalSetting 单独管理，固定放在 %LOCALAPPDATA%\EllenLauncher。
               数据目录里那份是历史副本，跟着复制会把当前配置覆盖成旧的。 */
            string fileName = Path.GetFileName(file);
            if (string.Equals(fileName, "settings.json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "settings.json.tmp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }
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