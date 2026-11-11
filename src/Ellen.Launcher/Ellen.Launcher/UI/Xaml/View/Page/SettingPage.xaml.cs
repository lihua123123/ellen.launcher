// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Picker;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.QuickLaunch;
using Ellen.Launcher.Service.Transfer;
using Ellen.Launcher.Service.UIGF;
using Ellen.Launcher.Service.Update;
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
/// 分组：游戏（两款游戏的安装目录）、图鉴图片资源、软件数据与缓存、导入导出、更新。
/// 布局沿用设置卡片的观感（分组标题 + 整块面板 + 行内分隔线 + 行右侧控件），
/// 宽度随窗口自适应，不新建 ViewModel（只用 LocalSetting / LaunchOptions，
/// 以及导入导出用的 scoped 服务，避免改动启动期 DI）。
/// </summary>
public sealed partial class SettingPage : ScopedPage
{
    public const string ImageQualityCompact = "compact";
    public const string ImageQualityOriginal = "original";

    private bool initialized;
    private bool migrating;
    private bool checkingUpdate;
    private bool transferBusy;
    private CheckUpdateResult? updateResult;

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

        // 游戏目录必须读写 LaunchOptions：它落在数据库设置里，
        // 与 LocalSetting（settings.json）不是同一个存储，直接写 LocalSetting 启动器读不到。
        UpdateGamePaths();

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

        // 便携布局下 InitializeDataDirectory 直接认 exe 旁的 portable.json，忽略
        // SettingKeys.DataDirectory —— 在这里再跑迁移只会白复制一份然后下次启动被无视，
        // 所以直接把这行关掉并说明原因。
        if (PortableLayout.IsEnabled)
        {
            MigrateCard.IsClickEnabled = false;
            MigrateCard.IsEnabled = false;
            MigrateHint.Text = "便携模式：数据存放在安装目录下的 Data 子目录，跟随安装位置。要换位置请重新安装到目标目录。";
        }

        // 更新：记住上次检查到的新版本，重启后仍能在设置页看到「有新版本」。
        AutoCheckSwitch.IsOn = LocalSetting.Get(SettingKeys.UpdateAutoCheck, false);

        string known = LocalSetting.Get(SettingKeys.UpdateKnownLatestVersion, string.Empty);
        if (!string.IsNullOrWhiteSpace(known)
            && System.Version.TryParse(known, out System.Version? knownVersion)
            && knownVersion > EllenRuntime.Version)
        {
            UpdateStatusText.Text = $"上次检查发现有新版本 {knownVersion}（当前 {EllenRuntime.Version}）。";
            DownloadHint.Text = $"在浏览器里打开发布页，手动下载 {knownVersion} 的安装包。";
            DownloadCard.Visibility = Visibility.Visible;
            DownloadDivider.Visibility = Visibility.Visible;
        }
    }

    private void OnAutoCheckToggled(object sender, RoutedEventArgs e)
    {
        // 初始化时赋值 IsOn 也会触发一次 Toggled，值没变就不要写设置
        if (LocalSetting.Get(SettingKeys.UpdateAutoCheck, false) == AutoCheckSwitch.IsOn)
        {
            return;
        }

        LocalSetting.Set(SettingKeys.UpdateAutoCheck, AutoCheckSwitch.IsOn);
    }

    private async void OnCheckNow(object sender, RoutedEventArgs e)
    {
        if (checkingUpdate)
        {
            return;
        }

        IServiceProvider? serviceProvider = XamlRoot.XamlContext()?.ServiceProvider;
        if (serviceProvider is null)
        {
            UpdateStatusText.Text = "检查失败：应用服务尚未就绪。";
            return;
        }

        checkingUpdate = true;
        CheckNowButton.IsEnabled = false;
        UpdateStatusText.Text = "正在检查…";

        try
        {
            IUpdateService updateService = serviceProvider.GetRequiredService<IUpdateService>();
            CheckUpdateResult result = await updateService.CheckUpdateAsync().ConfigureAwait(true);
            updateResult = result;

            switch (result.Kind)
            {
                case CheckUpdateResultKind.UpdateAvailable:
                    UpdateStatusText.Text = $"发现新版本 {result.LatestVersion}（当前 {EllenRuntime.Version}）。";
                    DownloadHint.Text = $"在浏览器里打开发布页，手动下载 {result.LatestVersion} 的安装包。";
                    DownloadCard.Visibility = Visibility.Visible;
                    DownloadDivider.Visibility = Visibility.Visible;
                    break;

                case CheckUpdateResultKind.AlreadyUpdated:
                    UpdateStatusText.Text = $"已是最新版本（{EllenRuntime.Version}）。";
                    DownloadCard.Visibility = Visibility.Collapsed;
                    DownloadDivider.Visibility = Visibility.Collapsed;
                    break;

                default:
                    UpdateStatusText.Text = string.IsNullOrWhiteSpace(result.Error)
                        ? "检查失败：所有更新源都不可达。"
                        : $"检查失败：{result.Error}";
                    DownloadCard.Visibility = Visibility.Collapsed;
                    DownloadDivider.Visibility = Visibility.Collapsed;
                    break;
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"检查失败：{ex.GetType().Name}";
        }
        finally
        {
            checkingUpdate = false;
            CheckNowButton.IsEnabled = true;
        }
    }

    private async void OnOpenDownloadPage(object sender, RoutedEventArgs e)
    {
        if (updateResult is null)
        {
            return;
        }

        IServiceProvider? serviceProvider = XamlRoot.XamlContext()?.ServiceProvider;
        if (serviceProvider is null)
        {
            return;
        }

        await serviceProvider.GetRequiredService<IUpdateService>()
            .OpenDownloadPageAsync(updateResult)
            .ConfigureAwait(true);
    }

    private LaunchOptions? ResolveLaunchOptions()
    {
        return XamlRoot.XamlContext()?.ServiceProvider.GetService<LaunchOptions>();
    }

    private void UpdateGamePaths()
    {
        LaunchOptions? options = ResolveLaunchOptions();
        if (options is null)
        {
            GenshinPathText.Text = "应用服务尚未就绪";
            ZzzPathText.Text = "应用服务尚未就绪";
            return;
        }

        GenshinPathText.Text = DescribeGamePath(options.GamePathEntry.Value?.Path);
        ZzzPathText.Text = DescribeGamePath(options.ZZZGamePath.Value);
    }

    private static string DescribeGamePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "未设置 · 点右侧「选择文件夹」指定游戏根目录" : path;
    }

    private void OnPickGenshinPath(object sender, RoutedEventArgs e)
    {
        PickGamePath(GenshinPathText, GameLaunchKind.Genshin, "选择原神安装目录");
    }

    private void OnPickZzzPath(object sender, RoutedEventArgs e)
    {
        PickGamePath(ZzzPathText, GameLaunchKind.ZenlessZoneZero, "选择绝区零安装目录");
    }

    private void PickGamePath(TextBlock target, GameLaunchKind kind, string title)
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

        // 用户选的是目录，落地时补成主程序完整路径；目录里确实没有主程序时如实提示，
        // 不假装保存成功 —— 否则首页会显示「未找到游戏」而用户以为自己已经设过了。
        bool foundExecutable = TryResolveGameExecutable(folder, kind, out string resolved);

        LaunchOptions options = serviceProvider.GetRequiredService<LaunchOptions>();
        try
        {
            if (kind is GameLaunchKind.Genshin)
            {
                // 这个入口会同时写「路径集合」和「当前选中项」，从首页搬过来后必须走同一条路，
                // 只写 SettingKeys.LaunchGamePath 的话 GamePathEntry 仍是 null，启动器读不到。
                options.PerformGamePathEntrySynchronization(resolved);
            }
            else
            {
                options.ZZZGamePath.Value = resolved;
            }
        }
        catch (Exception ex)
        {
            target.Text = $"保存失败：{ex.Message}";
            return;
        }

        target.Text = foundExecutable
            ? resolved
            : $"{resolved}（该目录下没找到游戏主程序，请确认选的是游戏根目录）";
    }

    private static bool TryResolveGameExecutable(string folder, GameLaunchKind kind, out string resolved)
    {
        string[] candidates = kind is GameLaunchKind.Genshin
            ? ["YuanShen.exe", "GenshinImpact.exe", @"Genshin Impact Game\YuanShen.exe", @"Genshin Impact Game\GenshinImpact.exe"]
            : [GameConstants.ZenlessZoneZeroFileName, @"ZenlessZoneZero Game\ZenlessZoneZero.exe"];

        foreach (string candidate in candidates)
        {
            string full = Path.Combine(folder, candidate);
            if (File.Exists(full))
            {
                resolved = full;
                return true;
            }
        }

        // 没有主程序：保留用户选的目录本身，让上游定位器再试一次（它支持传目录）
        resolved = folder;
        return false;
    }

    private void OnOpenGenshinFolder(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(ResolveLaunchOptions()?.GamePathEntry.Value?.Path);
    }

    private void OnOpenZzzFolder(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(ResolveLaunchOptions()?.ZZZGamePath.Value);
    }

    private void OnOpenDataDirectory(object sender, RoutedEventArgs e)
    {
        OpenInExplorer(EllenRuntime.DataDirectory);
    }

    private static void OpenInExplorer(string? path)
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
            // 复用原有的迁移操作：选目录 → 记录 Pending → 下次启动（数据库尚未打开时）复制。
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

            string target = LocalSetting.Get(SettingKeys.PendingDataDirectory, string.Empty);
            await ShowMessageAsync(
                "已记录新的数据目录",
                $"重启后会复制到：{target}\n\n"
                + "复制发生在下次启动、数据库尚未打开时，所以现在的记录不会丢。\n"
                + "旧目录会原样保留作为备份，确认新目录没问题后你可以自行删除。").ConfigureAwait(true);
        }
        finally
        {
            migrating = false;
        }
    }

    // ── 导入导出 ────────────────────────────────────────────────────────────
    // 这些操作原本散在「祈愿记录」「成就」两页的工具栏里，现在统一收到设置页。
    // 实现在 Service.Transfer 下，两个页面不再各自维护一份。

    private async void OnImportGachaLog(object sender, RoutedEventArgs e)
    {
        await RunTransferAsync<IGachaLogTransferService>(service => service.ImportAsync()).ConfigureAwait(true);
    }

    private async void OnExportGachaLog(object sender, RoutedEventArgs e)
    {
        // 原祈愿页的导出固定用 4.2（页面上没有版本选择器），这里保持一致。
        await RunTransferAsync<IGachaLogTransferService>(service => service.ExportAsync(UIGFVersion.UIGF42)).ConfigureAwait(true);
    }

    private async void OnImportAchievementFromFile(object sender, RoutedEventArgs e)
    {
        await RunTransferAsync<IAchievementTransferService>(service => service.ImportFromFileAsync()).ConfigureAwait(true);
    }

    private async void OnImportAchievementFromClipboard(object sender, RoutedEventArgs e)
    {
        await RunTransferAsync<IAchievementTransferService>(service => service.ImportFromClipboardAsync()).ConfigureAwait(true);
    }

    private async void OnImportAchievementFromYae(object sender, RoutedEventArgs e)
    {
        await RunTransferAsync<IAchievementTransferService>(service => service.ImportFromEmbeddedYaeAsync()).ConfigureAwait(true);
    }

    private async void OnExportAchievement(object sender, RoutedEventArgs e)
    {
        await RunTransferAsync<IAchievementTransferService>(service => service.ExportAsync()).ConfigureAwait(true);
    }

    /// <summary>
    /// 导入导出服务都是 Scoped，且依赖 DbContext。这里临时开一个作用域，
    /// 用完即弃，不去碰页面自身的作用域（设置页没有 ViewModel，也没有别的 scoped 依赖）。
    /// </summary>
    private async Task RunTransferAsync<TService>(Func<TService, ValueTask<bool>> action)
        where TService : class
    {
        if (transferBusy)
        {
            return;
        }

        IServiceProvider? root = XamlRoot.XamlContext()?.ServiceProvider;
        if (root is null)
        {
            return;
        }

        transferBusy = true;
        try
        {
            using IServiceScope scope = root.CreateScope();
            TService service = scope.ServiceProvider.GetRequiredService<TService>();
            await action(service).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("操作没有完成", $"{ex.GetType().Name}：{ex.Message}").ConfigureAwait(true);
        }
        finally
        {
            transferBusy = false;
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
