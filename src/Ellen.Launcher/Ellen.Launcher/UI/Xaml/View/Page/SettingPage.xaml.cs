// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Common;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.UI.Content;
using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Setting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Ellen.Launcher.UI.Xaml.View.Page;

/// <summary>
/// 「设置」页。历史上本 fork 把整个设置页删掉了（NavigationView 只剩
/// <c>IsSettingsVisible="False"</c>），但底层 SettingKeys、文件夹选择器、
/// 数据目录迁移操作和样式资源都还在，这里把它们重新接成可用 UI。
///
/// 布局参照 ky3 Launcher：品牌头卡 + 分组标题 + 整块圆角面板（行内分隔线），
/// 每行是「图标 + 标题 / 说明 + 右侧控件」。四组：游戏安装目录 / 图鉴图片资源 /
/// 存储空间（数据、缓存、安装目录 + 数据目录迁移）。
/// 不新建 ViewModel（只用 LocalSetting，避免改动启动期 DI）。
/// </summary>
public sealed partial class SettingPage : ScopedPage
{
    public const string ImageQualityCompact = "compact";
    public const string ImageQualityOriginal = "original";

    private bool initialized;
    private bool migrating;

    public SettingPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private static string BundledDirectory => Path.Combine(AppContext.BaseDirectory, @"Resource\EnemyArchives\Web753");

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        if (initialized)
        {
            return;
        }

        initialized = true;

        VersionText.Text = $"版本 {EllenRuntime.Version}";

        UpdateGamePathText(GenshinPathText, SettingKeys.LaunchGamePath);
        UpdateGamePathText(ZzzPathText, SettingKeys.LaunchZZZGamePath);

        // 安装器把档位写进 portable.json；首次打开设置页时同步成正式设置项。
        string imageQuality = LocalSetting.Get(SettingKeys.EnemyArchiveImageQuality, string.Empty);
        if (string.IsNullOrEmpty(imageQuality))
        {
            imageQuality = PortableLayout.ImageQuality ?? ImageQualityCompact;
            LocalSetting.Set(SettingKeys.EnemyArchiveImageQuality, imageQuality);
        }

        ImageQualityBox.SelectedIndex = string.Equals(imageQuality, ImageQualityOriginal, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        UpdateImageQualityHint();

        DataPathText.Text = EllenRuntime.DataDirectory;
        CachePathText.Text = EllenRuntime.LocalCacheDirectory;
        InstallPathText.Text = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        _ = RefreshStorageInformationAsync();
    }

    private static void UpdateGamePathText(TextBlock target, string settingKey)
    {
        string value = LocalSetting.Get(settingKey, string.Empty);
        target.Text = string.IsNullOrWhiteSpace(value) ? "未设置（点击右侧「选择文件夹」指定游戏根目录）" : value;
    }

    private async Task RefreshStorageInformationAsync()
    {
        (long dataSize, long cacheSize, long installSize) = await Task.Run(() =>
        {
            long data = GetDirectorySize(EllenRuntime.DataDirectory);
            long cache = GetDirectorySize(EllenRuntime.LocalCacheDirectory);
            long install = GetDirectorySize(AppContext.BaseDirectory);
            return (data, cache, install);
        }).ConfigureAwait(true);

        DataSizeText.Text = $"已使用磁盘空间：{Converters.ToFileSizeString(dataSize)}";
        CacheSizeText.Text = $"已使用磁盘空间：{Converters.ToFileSizeString(cacheSize)}";
        InstallSizeText.Text = $"已使用磁盘空间：{Converters.ToFileSizeString(installSize)}";

        (int count, int images, long bytes) = await Task.Run(ReadBundledSummary).ConfigureAwait(true);
        BundledText.Text = count > 0
            ? $"随包 Web753 数据：{count} 条图鉴 · {images:N0} 张图片 · {Converters.ToFileSizeString(bytes)}"
            : "未找到随包图鉴数据（Resource\\EnemyArchives\\Web753）";
    }

    private static (int Count, int Images, long Bytes) ReadBundledSummary()
    {
        string directory = BundledDirectory;
        if (!Directory.Exists(directory))
        {
            return (0, 0, 0);
        }

        int images = 0;
        long bytes = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                    if (file.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                    {
                        images++;
                    }
                }
                catch (IOException)
                {
                    // 统计失败的文件跳过，不影响页面
                }
            }
        }
        catch (Exception)
        {
            // 目录不可读时如实降级为空统计
        }

        int count = 0;
        string bundled = Path.Combine(directory, "bundled-all.json");
        if (File.Exists(bundled))
        {
            try
            {
                using FileStream stream = File.OpenRead(bundled);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty("count", out JsonElement element) && element.TryGetInt32(out int value))
                {
                    count = value;
                }
            }
            catch (JsonException)
            {
                // 数据结构异常时只显示文件统计
            }
        }

        return (count, images, bytes);
    }

    private static long GetDirectorySize(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return 0;
        }

        long total = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                    // 单个文件读不到就跳过
                }
            }
        }
        catch (Exception)
        {
            // 目录枚举失败按 0 处理
        }

        return total;
    }

    private void OnPickGenshinPath(object sender, RoutedEventArgs e)
    {
        PickGamePath(GenshinPathText, SettingKeys.LaunchGamePath, "选择原神安装目录");
    }

    private void OnPickZzzPath(object sender, RoutedEventArgs e)
    {
        PickGamePath(ZzzPathText, SettingKeys.LaunchZZZGamePath, "选择绝区零安装目录");
    }

    private void PickGamePath(TextBlock target, string settingKey, string title)
    {
        IServiceProvider? serviceProvider = XamlRoot.XamlContext()?.ServiceProvider;
        if (serviceProvider is null)
        {
            return;
        }

        IFileSystemPickerInteraction picker = serviceProvider.GetRequiredService<IFileSystemPickerInteraction>();
        if (!picker.PickFolder(title).TryGetValue(out string? folder) || string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        // LaunchOptions.GamePathEntry / ZZZGamePath 期望的是可执行文件完整路径，
        // 用户选的是目录时帮它补上主程序；找不到就如实保留目录本身，不编造。
        string resolved = ResolveGameExecutable(folder);
        target.Text = resolved;
        LocalSetting.Set(settingKey, resolved);
    }

    private static string ResolveGameExecutable(string folder)
    {
        string[] candidates =
        [
            "YuanShen.exe",
            "GenshinImpact.exe",
            "ZenlessZoneZero.exe",
            @"Genshin Impact Game\YuanShen.exe",
            @"Genshin Impact Game\GenshinImpact.exe",
        ];

        foreach (string candidate in candidates)
        {
            string full = Path.Combine(folder, candidate);
            if (File.Exists(full))
            {
                return full;
            }
        }

        return folder;
    }

    private void OnOpenGenshinFolder(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(LocalSetting.Get(SettingKeys.LaunchGamePath, string.Empty));
    }

    private void OnOpenZzzFolder(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(LocalSetting.Get(SettingKeys.LaunchZZZGamePath, string.Empty));
    }

    private void OnOpenDataDirectory(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(EllenRuntime.DataDirectory);
    }

    private void OnOpenCacheDirectory(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(EllenRuntime.LocalCacheDirectory);
    }

    private void OnOpenInstallDirectory(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(AppContext.BaseDirectory);
    }

    private static void OpenInExplorer(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            string target = File.Exists(path) ? Path.GetDirectoryName(path) ?? path : path;
            if (!Directory.Exists(target))
            {
                return;
            }

            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // 打开资源管理器失败不影响设置页
        }
    }

    private void OnImageQualityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized)
        {
            return;
        }

        string value = ImageQualityBox.SelectedIndex is 1 ? ImageQualityOriginal : ImageQualityCompact;
        LocalSetting.Set(SettingKeys.EnemyArchiveImageQuality, value);
        UpdateImageQualityHint();
    }

    private void UpdateImageQualityHint()
    {
        ImageQualityHint.Text = ImageQualityBox.SelectedIndex is 1
            ? "一图流原图（1920×1080 PNG，上游 624 张），安装包默认不带；下载与图鉴读取链路尚未接通，当前仍按随包 webp 显示。"
            : "随包的 1600px webp 一图流，离线可用，安装包约 249 MB。";
    }

    private async void OnMigrateDataDirectory(object sender, RoutedEventArgs e)
    {
        if (migrating)
        {
            return;
        }

        IServiceProvider? serviceProvider = XamlRoot.XamlContext()?.ServiceProvider;
        if (serviceProvider is null)
        {
            return;
        }

        migrating = true;
        try
        {
            // 复用原有的迁移操作：选目录 → 复制整个旧目录 → 标记旧目录下次启动删除 → 写入新 DataDirectory。
            SettingStorageSetDataFolderOperation operation = new()
            {
                FileSystemPickerInteraction = serviceProvider.GetRequiredService<IFileSystemPickerInteraction>(),
                ContentDialogFactory = serviceProvider.GetRequiredService<IContentDialogFactory>(),
                Messenger = serviceProvider.GetRequiredService<IMessenger>(),
            };

            bool migrated = await operation.TryExecuteAsync().ConfigureAwait(true);
            if (!migrated)
            {
                return;
            }

            string target = LocalSetting.Get(SettingKeys.DataDirectory, EllenRuntime.DataDirectory);
            DataPathText.Text = $"{target}（重启后生效）";
            await ShowMessageAsync("迁移完成", $"数据已复制到：{target}\n请重启应用后生效；旧目录会在下次启动时清理。").ConfigureAwait(true);
        }
        finally
        {
            migrating = false;
        }
    }

    private async Task ShowMessageAsync(string title, string content)
    {
        await new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = "确定",
        }.ShowAsync();
    }
}
