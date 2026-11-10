// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Win32;
using Ellen.Launcher.Core.IO.Ini;
using Ellen.Launcher.Service.Game.Locator;
using Ellen.Launcher.Service.Game.PathAbstraction;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.Game.QuickLaunch;

/// <summary>
/// 游戏安装位置识别。识别顺序参考 Starward：
/// 1. 用户在启动器里手动指定过的路径；
/// 2. 官方启动器在卸载表里登记的安装目录，再读其 config.ini 的 game_install_path；
/// 3. Unity 输出日志（仅原神）。
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IGameInstallationLocator))]
public sealed partial class GameInstallationLocator : IGameInstallationLocator
{
    private const string UninstallKeyPathFormat = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{0}";
    private const string LauncherInstallPathValueName = "InstallPath";

    private static readonly string[] GenshinLauncherKeyNames = ["原神", "Genshin Impact"];
    private static readonly string[] GenshinExecutableNames = [GameConstants.YuanShenFileName, GameConstants.GenshinImpactFileName];
    private static readonly string[] GenshinDataDirectoryNames = [GameConstants.YuanShenData, GameConstants.GenshinImpactData];

    private static readonly string[] ZenlessZoneZeroLauncherKeyNames = ["绝区零", "ZenlessZoneZero", "Zenless Zone Zero"];
    private static readonly string[] ZenlessZoneZeroExecutableNames = [GameConstants.ZenlessZoneZeroFileName];
    private static readonly string[] ZenlessZoneZeroDataDirectoryNames = [GameConstants.ZenlessZoneZeroData];

    private readonly LaunchOptions launchOptions;
    private readonly IGameLocatorFactory gameLocatorFactory;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial GameInstallationLocator(IServiceProvider serviceProvider);

    public async ValueTask<GameInstallation> LocateAsync(GameLaunchKind kind, CancellationToken token = default)
    {
        await taskContext.SwitchToBackgroundAsync();
        token.ThrowIfCancellationRequested();

        return kind switch
        {
            GameLaunchKind.Genshin => await LocateGenshinAsync(token).ConfigureAwait(false),
            GameLaunchKind.ZenlessZoneZero => LocateZenlessZoneZero(),
            _ => GameInstallation.NotFound(kind),
        };
    }

    private async ValueTask<GameInstallation> LocateGenshinAsync(CancellationToken token)
    {
        // 1. 启动器内已保存的路径（可能包含历史路径，逐个校验可执行文件是否存在）
        if (LocateFromConfiguredEntries(GenshinExecutableNames) is { } configured)
        {
            return CreateInstallation(GameLaunchKind.Genshin, configured, GameInstallSource.UserConfigured, GenshinDataDirectoryNames);
        }

        // 2. 官方启动器的卸载表项 + config.ini
        if (LocateFromLauncherRegistry(GenshinLauncherKeyNames, GenshinExecutableNames) is { } fromRegistry)
        {
            return CreateInstallation(GameLaunchKind.Genshin, fromRegistry, GameInstallSource.Registry, GenshinDataDirectoryNames);
        }

        // 3. Unity 输出日志兜底
        token.ThrowIfCancellationRequested();
        if (await gameLocatorFactory.LocateSingleAsync(GameLocationSourceKind.UnityLog).ConfigureAwait(false) is (true, { } logPath)
            && IsGenshinExecutable(logPath))
        {
            return CreateInstallation(GameLaunchKind.Genshin, logPath, GameInstallSource.UnityLog, GenshinDataDirectoryNames);
        }

        return GameInstallation.NotFound(GameLaunchKind.Genshin);
    }

    private GameInstallation LocateZenlessZoneZero()
    {
        // 1. 首页「设置绝区零游戏路径」保存的路径
        string configured = launchOptions.ZZZGamePath.Value;
        if (FindExecutable(configured, ZenlessZoneZeroExecutableNames) is { } configuredExecutable)
        {
            return CreateInstallation(GameLaunchKind.ZenlessZoneZero, configuredExecutable, GameInstallSource.UserConfigured, ZenlessZoneZeroDataDirectoryNames);
        }

        // 2. 官方启动器的卸载表项 + config.ini
        if (LocateFromLauncherRegistry(ZenlessZoneZeroLauncherKeyNames, ZenlessZoneZeroExecutableNames) is { } fromRegistry)
        {
            return CreateInstallation(GameLaunchKind.ZenlessZoneZero, fromRegistry, GameInstallSource.Registry, ZenlessZoneZeroDataDirectoryNames);
        }

        return GameInstallation.NotFound(GameLaunchKind.ZenlessZoneZero);
    }

    private static bool IsGenshinExecutable(string path)
    {
        return FindExecutable(path, GenshinExecutableNames) is not null;
    }

    private string? LocateFromConfiguredEntries(string[] executableNames)
    {
        foreach (GamePathEntry entry in launchOptions.GamePathEntries.Value)
        {
            if (FindExecutable(entry.Path, executableNames) is { } executable)
            {
                return executable;
            }
        }

        // 当前选中项可能尚未同步进 entries
        return FindExecutable(launchOptions.GamePathEntry.Value?.Path, executableNames);
    }

    private static string? LocateFromLauncherRegistry(string[] launcherKeyNames, string[] executableNames)
    {
        foreach (string launcherKeyName in launcherKeyNames)
        {
            foreach (RegistryHive hive in (ReadOnlySpan<RegistryHive>)[RegistryHive.LocalMachine, RegistryHive.CurrentUser])
            {
                foreach (RegistryView view in (ReadOnlySpan<RegistryView>)[RegistryView.Registry64, RegistryView.Registry32])
                {
                    if (ReadLauncherInstallDirectory(launcherKeyName, hive, view) is not { } launcherDirectory)
                    {
                        continue;
                    }

                    if (ReadGameInstallDirectory(launcherDirectory) is not { } installDirectory)
                    {
                        continue;
                    }

                    if (FindExecutable(installDirectory, executableNames) is { } executable)
                    {
                        return executable;
                    }
                }
            }
        }

        return null;
    }

    private static string? ReadLauncherInstallDirectory(string launcherKeyName, RegistryHive hive, RegistryView view)
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
            using RegistryKey? uninstallKey = baseKey.OpenSubKey(string.Format(UninstallKeyPathFormat, launcherKeyName));

            if (uninstallKey?.GetValue(LauncherInstallPathValueName) is not string launcherDirectory)
            {
                return null;
            }

            return Directory.Exists(launcherDirectory) ? launcherDirectory : null;
        }
        catch (Exception)
        {
            // 权限不足 / 注册表视图不存在
            return null;
        }
    }

    /// <summary>
    /// 官方启动器允许把游戏安装在启动器目录之外，真实位置记录在启动器目录的 config.ini 中。
    /// </summary>
    private static string? ReadGameInstallDirectory(string launcherDirectory)
    {
        string? installPath = ReadIniValue(Path.Combine(launcherDirectory, GameConstants.ConfigFileName), "game_install_path");
        if (string.IsNullOrEmpty(installPath))
        {
            return null;
        }

        try
        {
            string fullPath = Path.GetFullPath(Path.IsPathRooted(installPath) ? installPath : Path.Combine(launcherDirectory, installPath));
            return Directory.Exists(fullPath) ? fullPath : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static GameInstallation CreateInstallation(GameLaunchKind kind, string executablePath, GameInstallSource source, string[] dataDirectoryNames)
    {
        return new()
        {
            Kind = kind,
            ExecutablePath = executablePath,
            Version = TryGetGameVersion(executablePath, dataDirectoryNames),
            Source = source,
        };
    }

    private static string? TryGetGameVersion(string executablePath, string[] dataDirectoryNames)
    {
        string? gameDirectory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrEmpty(gameDirectory))
        {
            return null;
        }

        string? version = ReadIniValue(Path.Combine(gameDirectory, GameConstants.ConfigFileName), GameConstants.GameVersion);
        if (!string.IsNullOrEmpty(version))
        {
            return version;
        }

        foreach (string dataDirectoryName in dataDirectoryNames)
        {
            string scriptVersionPath = Path.Combine(gameDirectory, dataDirectoryName, "Persistent", "ScriptVersion");
            if (!File.Exists(scriptVersionPath))
            {
                continue;
            }

            try
            {
                string content = File.ReadAllText(scriptVersionPath).Trim();
                if (content.Length > 0)
                {
                    return content;
                }
            }
            catch (Exception)
            {
                // 文件被占用时忽略
            }
        }

        return null;
    }

    /// <summary>
    /// 在路径（可执行文件或安装目录）中寻找指定名称的可执行文件。
    /// </summary>
    private static string? FindExecutable(string? path, string[] executableNames)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        try
        {
            if (Directory.Exists(path))
            {
                foreach (string executableName in executableNames)
                {
                    string candidate = Path.Combine(path, executableName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            if (!File.Exists(path))
            {
                return null;
            }

            string fileName = Path.GetFileName(path);
            foreach (string executableName in executableNames)
            {
                if (string.Equals(fileName, executableName, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
        }
        catch (Exception)
        {
            // 非法路径
        }

        return null;
    }

    private static string? ReadIniValue(string filePath, string key)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            ImmutableArray<IniElement> elements = IniSerializer.DeserializeFromFile(filePath);
            foreach (IniElement element in elements)
            {
                switch (element)
                {
                    case IniSection section:
                        foreach (IniElement child in section.Children)
                        {
                            if (child is IniParameter { Value.Length: > 0 } parameter && parameter.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                            {
                                return parameter.Value;
                            }
                        }

                        break;
                    case IniParameter { Value.Length: > 0 } parameter when parameter.Key.Equals(key, StringComparison.OrdinalIgnoreCase):
                        return parameter.Value;
                }
            }
        }
        catch (Exception)
        {
            // config.ini 损坏或不可读
        }

        return null;
    }
}
