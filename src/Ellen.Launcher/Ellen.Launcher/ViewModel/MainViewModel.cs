// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Snap Hutao RP. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.LifeCycle;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.BackgroundActivity;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.Network;
using Ellen.Launcher.Service.SignIn;
using Ellen.Launcher.Service.Update;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.UI.Xaml;
using Ellen.Launcher.UI.Xaml.Behavior.Action;
using Ellen.Launcher.UI.Xaml.Control.Theme;
using Ellen.Launcher.UI.Xaml.View.Window.WebView2;
using Ellen.Launcher.Web;
using Ellen.Launcher.Web.Request.Builder;
using System.Net.Http;
using System.IO;

namespace Ellen.Launcher.ViewModel;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
public sealed partial class MainViewModel : Abstraction.ViewModel, IDisposable
{
    private readonly ICurrentXamlWindowReference currentXamlWindowReference;
    private readonly IMetadataService metadataService;
    private readonly IUpdateService updateService;
    private readonly IUserService userService;
    private readonly IAutoSignInService autoSignInService;
    private readonly IGachaEventAutoUpdater gachaEventAutoUpdater;
    private readonly INetworkRetryCoordinator networkRetryCoordinator;
    private readonly IToastNotificationService toastNotificationService;
    private readonly IMessenger messenger;
    private readonly App app;
    private DispatcherTimer? successHideTimer;
    private IDisposable? startupRetryRegistration;

    [GeneratedConstructor]
    public partial MainViewModel(IServiceProvider serviceProvider);

    public static string? Title { get => EllenRuntime.GetDisplayName(); }

    public static bool IsDebug
    {
        get =>
#if DEBUG
            true;
#else
            false;
#endif
    }

    public partial AppOptions AppOptions { get; }

    public partial BackgroundActivityOptions BackgroundActivityOptions { get; }

    [ObservableProperty]
    public partial bool IsFlyoutOpen { get; set; }

    public override void Dispose()
    {
        using (CriticalSection.Enter())
        {
            successHideTimer?.Stop();
            BackgroundActivityOptions.MetadataInitialization.PropertyChanged -= OnMetadataInitializationPropertyChanged;
            startupRetryRegistration?.Dispose();
            Uninitialize();
        }

        base.Dispose();
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        BackgroundActivityOptions.MetadataInitialization.PropertyChanged += OnMetadataInitializationPropertyChanged;
        startupRetryRegistration ??= networkRetryCoordinator.Register("MainViewModel.Startup", RetryStartupAsync);

        NotifyIfDataFolderHasReparsePoint();
        await RetryStartupAsync(token).ConfigureAwait(false);

        return true;
    }

    private async ValueTask CheckUpdateAsync()
    {
        // 只在用户主动打开「启动时自动检查更新」后才联网比对版本；
        // 手动「立即检查」走设置页，不受这个开关限制。
        if (!LocalSetting.Get(SettingKeys.UpdateAutoCheck, false))
        {
            SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateDebug("Auto update check disabled", "MainViewModel.Command"));
            return;
        }

        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Check for update", "MainViewModel.Command"));

        CheckUpdateResult result = await updateService.CheckUpdateAsync().ConfigureAwait(false);
        if (result.Kind is not CheckUpdateResultKind.UpdateAvailable)
        {
            return;
        }

        // 右下角通知，不弹窗：更新是「顺带知道」的信息，不该打断用户手上的事。
        try
        {
            toastNotificationService.ShowText($"发现新版本 {result.LatestVersion}（当前 {EllenRuntime.Version}）。打开「设置 → 更新」可前往下载。");
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
        }
    }

    private void NotifyIfDataFolderHasReparsePoint()
    {
        if (new DirectoryInfo(EllenRuntime.DataDirectory).Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateDebug("Data folder has reparse point", "MainViewModel.Command"));
            messenger.Send(InfoBarMessage.Warning(SH.FormatViewModelTitleDataFolderHasReparsepoint(EllenRuntime.DataDirectory)));
        }
    }

    private void OnMetadataInitializationPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BackgroundActivity.IsCompletedSuccessfully))
        {
            BackgroundActivity? backgroundActivity = sender as BackgroundActivity;
            if (backgroundActivity?.IsCompletedSuccessfully == true)
            {
                StartSuccessHideTimer();
            }
        }
    }

    private void StartSuccessHideTimer()
    {
        successHideTimer?.Stop();
        successHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        successHideTimer.Tick += (s, e) =>
        {
            BackgroundActivityOptions.MetadataInitialization.ResetCompletionStatus();
            successHideTimer?.Stop();
        };
        successHideTimer.Start();
    }

    private async ValueTask<bool> RetryStartupAsync(CancellationToken token)
    {
        if (!await networkRetryCoordinator.HasInternetAccessAsync(token).ConfigureAwait(false))
        {
            networkRetryCoordinator.MarkPending("MainViewModel.Startup", SH.ViewModelMainNetworkUnavailableWillAutoRetry);
            return false;
        }

        try
        {
            if (!await metadataService.InitializepublicAsync(token).ConfigureAwait(false))
            {
                networkRetryCoordinator.MarkPending("MainViewModel.Startup", SH.ViewModelMainNetworkConnectionFailedWillAutoRetry);
                return false;
            }

            await userService.RetryResumeUninitializedUsersAsync(token).ConfigureAwait(false);

            // Auto sign-in runs silently, only failures surface a dialog. It must not block the
            // startup path, and it is not tied to this view's lifetime.
            autoSignInService.SignInAllAsync().SafeForget();

            // 新版本卡池的数据与图片在后台自动补齐，不能让网络拖慢首屏，失败也不影响现有数据。
            gachaEventAutoUpdater.UpdateInBackgroundAsync().SafeForget();

            // The update check is not required for the window to become usable. Run it without
            // blocking the startup path so a slow network never delays the first paint.
            CheckUpdateAsync().SafeForget();

            networkRetryCoordinator.ClearPending("MainViewModel.Startup");
            return true;
        }
        catch (Exception ex)
        {
            if (IsNetworkRelatedException(ex))
            {
                networkRetryCoordinator.MarkPending("MainViewModel.Startup", SH.ViewModelMainNetworkConnectionFailedWillAutoRetry);
                return false;
            }

            SentrySdk.CaptureException(ex);
            throw;
        }
    }

    private static bool IsNetworkRelatedException(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpRequestException => HttpRequestExceptionHandling.HttpRequestExceptionToNetworkError(httpRequestException) is not NetworkError.NULL,
            TimeoutException => true,
            TaskCanceledException => true,
            _ => false,
        };
    }
}