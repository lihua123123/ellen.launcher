// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

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

namespace Ellen.Launcher.UI.Xaml.View.Page;

/// <summary>
/// 「设置」页。历史上本 fork 把整个设置页删掉了（NavigationView 只剩
/// <c>IsSettingsVisible="False"</c>），但底层 SettingKeys、文件夹选择器、
/// 数据目录迁移操作和样式资源都还在，这里把它们重新接成可用 UI。
///
/// 只放三件事：两款游戏的安装目录、图鉴图片资源档位、数据与缓存目录迁移。
/// 布局沿用设置卡片的观感（分组标题 + 整块面板 + 行内分隔线 + 行右侧控件），
/// 宽度随窗口自适应，不新建 ViewModel（只用 LocalSetting，避免改动启动期 DI）。
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
    }

    private static void UpdateGamePathText(TextBlock target, string settingKey)
    {
        string value = LocalSetting.Get(settingKey, string.Empty);
        target.Text = string.IsNullOrWhiteSpace(value) ? "未设置 · 点右侧「选择文件夹」指定游戏根目录" : value;
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
