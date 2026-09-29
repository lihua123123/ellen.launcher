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
    private readonly INetworkRetryCoordinator networkRetryCoordinator;
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

        ShowUpdateLogWindowAfterUpdate();
        NotifyIfDataFolderHasReparsePoint();
        await RetryStartupAsync(token).ConfigureAwait(false);

        return true;
    }

    [Command("InvertAppThemeCommand")]
    private void InvertAppTheme()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Invert app theme", "MainViewModel.Command"));
        FrameworkTheming.SetTheme(ThemeHelper.ApplicationToFrameworkInvert(app.RequestedTheme));
    }

    private void ShowUpdateLogWindowAfterUpdate()
    {
        // Ellen.Launcher 有意不弹「更新日志」窗口：本 fork 已禁用在线更新（见 CheckUpdateAsync），
        // 而该窗口的内容源指向上游项目公告页
        // （UpdateLogContentProvider → {ServerDomain.GetRootDomain()}/statements/latest.html），
        // 对 Ellen 用户只会是「上游 release notes」的困惑或一个 404。
        // 将来若有自建公告页：在 UpdateLogContentProvider 里改 URL，并解除此与在线更新开关的绑定。
        if (!LocalSetting.Get(UpdateService.UpdateEnabledSettingKey, false))
        {
            return;
        }

        if (LocalSetting.Get(SettingKeys.AlwaysIsFirstRunAfterUpdate, false) || XamlApplicationLifetime.IsFirstRunAfterUpdate)
        {
            // Check if the window showed, only set to false if it is shown
            if (ShowWebView2WindowAction.TryShow<UpdateLogContentProvider>(currentXamlWindowReference.XamlRoot) is not null)
            {
                SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Show update log window", "MainViewModel.Command"));
                XamlApplicationLifetime.IsFirstRunAfterUpdate = false;
            }
        }
    }

    private async ValueTask CheckUpdateAsync()
    {
        // Online update is intentionally disabled for this fork: the upstream feed distributes the
        // upstream build and would overwrite Ellen Launcher. Updates are installed manually.
        if (!LocalSetting.Get(UpdateService.UpdateEnabledSettingKey, false))
        {
            SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateDebug("Online update disabled", "MainViewModel.Command"));
            return;
        }

        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Check for update", "MainViewModel.Command"));

        CheckUpdateResult checkUpdateResult = await updateService.CheckUpdateAsync().ConfigureAwait(false);
        await updateService.TriggerUpdateAsync(checkUpdateResult).ConfigureAwait(false);
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