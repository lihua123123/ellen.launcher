// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.LifeCycle.InterProcess;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Service.EllenWiki;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.UI.Windowing;
using Ellen.Launcher.UI.Xaml.View.Page;
using Ellen.Launcher.UI.Xaml.View.Window;
using Ellen.Launcher.ViewModel.Achievement;
using Ellen.Launcher.ViewModel.Game;
using Ellen.Launcher.ViewModel.Guide;
using Ellen.Launcher.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.Core.LifeCycle;

[Service(ServiceLifetime.Singleton, typeof(IAppActivation))]
[SuppressMessage("", "CA1001")]
public sealed partial class AppActivation : IAppActivation
{
    public const string Action = nameof(Action);
    public const string Uid = nameof(Uid);
    public const string LaunchGame = nameof(LaunchGame);

    private const string CategoryAchievement = "ACHIEVEMENT";
    private const string CategoryLaunch = "LAUNCH";
    private const string UrlActionImport = "/IMPORT";

    private readonly ICurrentXamlWindowReference currentXamlWindowReference;
    private readonly IServiceProvider serviceProvider;
    private readonly ITaskContext taskContext;

    private readonly AsyncLock activateLock = new();
    private int isActivating;

    [GeneratedConstructor]
    public partial AppActivation(IServiceProvider serviceProvider);

    public void RedirectedActivate(EllenActivationArguments args)
    {
        HandleActivationExclusivelyAsync(args).SafeForget();

        async ValueTask HandleActivationExclusivelyAsync(EllenActivationArguments args)
        {
            if (Interlocked.CompareExchange(ref isActivating, 1, 0) is not 0)
            {
                return;
            }

            using (await activateLock.LockAsync().ConfigureAwait(false))
            {
                await UnsynchronizedHandleActivationAsync(args).ConfigureAwait(false);
            }

            // Bring the main window to foreground after handling redirect activation
            await taskContext.SwitchToMainThreadAsync();
            if (currentXamlWindowReference.Window is { } window)
            {
                window.SwitchTo();
                window.AppWindow?.MoveInZOrderAtTop();
                window.Activate();
            }

            Interlocked.Exchange(ref isActivating, 0);
        }
    }

    public void NotificationInvoked(AppNotificationManager manager, AppNotificationActivatedEventArgs args)
    {
        HandleAppNotificationActivationAsync(args.Arguments.AsReadOnly(), false).SafeForget();
    }

    public void ActivateAndInitialize(EllenActivationArguments args)
    {
        if (Volatile.Read(ref isActivating) is 1)
        {
            return;
        }

        PrivateActivateAndInitializeAsync().SafeForget();

        async ValueTask PrivateActivateAndInitializeAsync()
        {
            try
            {
                using (await activateLock.LockAsync().ConfigureAwait(false))
                {
                    if (Interlocked.CompareExchange(ref isActivating, 1, 0) is not 0)
                    {
                        return;
                    }

                    await UnsynchronizedHandleActivationAsync(args).ConfigureAwait(false);
                    await UnsynchronizedHandleInitializationAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                // Activation failure is fatal — the process would run without a window.
                // Show error and terminate instead of silently swallowing it.
                EllenNative.Instance.ShowErrorMessage("Activation Error", ex.ToString());
                SentrySdk.CaptureException(ex);
                await SentrySdk.FlushAsync();
                ProcessFactory.KillCurrent();
            }
            finally
            {
                XamlApplicationLifetime.ActivationAndInitializationCompleted = true;
                Interlocked.Exchange(ref isActivating, 0);
            }
        }
    }

    private async ValueTask UnsynchronizedHandleActivationAsync(EllenActivationArguments args)
    {
        await taskContext.SwitchToBackgroundAsync();
        switch (args.Kind)
        {
            case EllenActivationKind.Protocol:
                {
                    ArgumentNullException.ThrowIfNull(args.ProtocolActivatedUri);
                    await HandleProtocolActivationAsync(args.ProtocolActivatedUri, args.IsRedirectTo).ConfigureAwait(false);
                    break;
                }

            case EllenActivationKind.Launch:
                {
                    ArgumentNullException.ThrowIfNull(args.LaunchActivatedArguments);
                    await HandleLaunchActivationAsync(args.IsRedirectTo).ConfigureAwait(false);
                    break;
                }

            case EllenActivationKind.AppNotification:
                {
                    ArgumentNullException.ThrowIfNull(args.AppNotificationActivatedArguments);
                    await HandleAppNotificationActivationAsync(args.AppNotificationActivatedArguments, args.IsRedirectTo).ConfigureAwait(false);
                    break;
                }
        }
    }

    private async ValueTask UnsynchronizedHandleInitializationAsync()
    {
        // Sentry IpAddress Traits, should always be configured
        // Telemetry is opt-in: skipping the enricher also avoids an extra network round trip.
        if (SentryTelemetry.IsEnabled)
        {
            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                // Transient, we need the scope to manage its lifetime
                await scope.ServiceProvider.GetRequiredService<SentryIpAddressEnricher>().ConfigureAsync().ConfigureAwait(false);
            }
        }

        // In guide
        if (UnsafeLocalSetting.Get(SettingKeys.GuideState, GuideState.Language) < GuideState.Completed)
        {
            return;
        }

        // Start named pipe server
        serviceProvider.GetRequiredService<PrivateNamedPipeServer>().Start();
        Bootstrap.UseNamedPipeRedirection();

        await taskContext.SwitchToBackgroundAsync();

        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateInfo("Core initialization completed", "Application"));

        // Phase 2: Non-critical background initialization — runs without blocking window readiness
        InitializeBackgroundServicesAsync().SafeForget();

        async ValueTask InitializeBackgroundServicesAsync()
        {
            await Task.WhenAll(
            [
                serviceProvider.GetRequiredService<IMetadataService>().InitializepublicAsync().AsTask(),
                serviceProvider.GetRequiredService<IEllenWikiDataService>().InitializeAsync().AsTask(),
            ]).ConfigureAwait(false);

            SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateInfo("Background initialization completed", "Application"));
        }
    }

    private async ValueTask HandleProtocolActivationAsync(Uri uri, bool isRedirectTo)
    {
        UriBuilder builder = new(uri);

        string category = builder.Host.ToUpperInvariant();
        string action = builder.Path.ToUpperInvariant();

        // string parameter = builder.Query.ToUpperInvariant();
        switch (category)
        {
            case CategoryAchievement:
                {
                    await WaitWindowAsync<MainWindow>().ConfigureAwait(false);
                    switch (action)
                    {
                        case UrlActionImport:
                            {
                                await taskContext.SwitchToMainThreadAsync();

                                INavigationCompletionSource navigationAwaiter = new NavigationExtraData(AchievementViewModel.ImportUIAFFromClipboard);
#pragma warning disable CA1849
                                // We can't await there to navigate to Achievement Page, the Achievement
                                // ViewModel requires the Metadata Service to be initialized.
                                // Which is initialized in there (AppActivation - Initialization) which is after Activation.
                                // Thus await would cause a deadlock.
                                // ReSharper disable once MethodHasAsyncOverload
                                serviceProvider
                                    .GetRequiredService<INavigationService>()
                                    .Navigate<AchievementPage>(navigationAwaiter, true);
#pragma warning restore CA1849
                                break;
                            }
                    }

                    break;
                }

            default:
                {
                    await HandleLaunchActivationAsync(isRedirectTo).ConfigureAwait(false);
                    break;
                }
        }
    }

    private static Dictionary<string, string?> ParseQueryString(string query)
    {
        Dictionary<string, string?> parameters = new(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query) || query.Length <= 1)
        {
            return parameters;
        }

        // Skip the leading '?' or '#'
        ReadOnlySpan<char> span = query.AsSpan();
        if (span[0] is '?' or '#')
        {
            span = span[1..];
        }

        while (!span.IsEmpty)
        {
            int ampIndex = span.IndexOf('&');
            ReadOnlySpan<char> pair = ampIndex >= 0 ? span[..ampIndex] : span;

            int eqIndex = pair.IndexOf('=');
            if (eqIndex > 0)
            {
                string key = pair[..eqIndex].ToString();
                string value = eqIndex < pair.Length - 1 ? pair[(eqIndex + 1)..].ToString() : string.Empty;
                parameters[key] = Uri.UnescapeDataString(value);
            }
            else if (eqIndex < 0 && pair.Length > 0)
            {
                parameters[pair.ToString()] = string.Empty;
            }

            span = ampIndex >= 0 ? span[(ampIndex + 1)..] : [];
        }

        return parameters;
    }

    private async ValueTask HandleLaunchActivationAsync(bool isRedirectTo)
    {
        if (isRedirectTo)
        {
            await WaitWindowAsync<MainWindow>().ConfigureAwait(false);
            return;
        }

        // Increase launch times
        LocalSetting.Update(SettingKeys.LaunchTimes, 0, static x => unchecked(x + 1));

        // If the guide is completed, we check if there's any unfulfilled resource category present.
        if (UnsafeLocalSetting.Get(SettingKeys.GuideState, GuideState.Language) >= GuideState.StaticResourceBegin)
        {
            if (StaticResource.IsAnyUnfulfilledCategoryPresent())
            {
                UnsafeLocalSetting.Set(SettingKeys.GuideState, GuideState.StaticResourceBegin);
            }
        }

        if (UnsafeLocalSetting.Get(SettingKeys.GuideState, GuideState.Language) < GuideState.Completed)
        {
            await WaitWindowAsync<GuideWindow>().ConfigureAwait(false);
            return;
        }

        if (Version.Parse(LocalSetting.Update(SettingKeys.LastVersion, "0.0.0.0", $"{EllenRuntime.Version}")) < EllenRuntime.Version)
        {
            // Note: If the user close MainWindow too quickly, and then exit app, he will never see the update log again.
            XamlApplicationLifetime.IsFirstRunAfterUpdate = true;
        }

        await WaitWindowAsync<MainWindow>().ConfigureAwait(false);
    }

    private async ValueTask HandleAppNotificationActivationAsync(IReadOnlyDictionary<string, string> arguments, bool isRedirectTo)
    {
        await HandleLaunchActivationAsync(isRedirectTo).ConfigureAwait(false);
    }

    private async ValueTask<Window?> WaitWindowAsync<TWindow>()
        where TWindow : Window
    {
        await taskContext.SwitchToMainThreadAsync();

        if (currentXamlWindowReference.Window is not { } window)
        {
            try
            {
                window = serviceProvider.GetRequiredService<TWindow>();
            }
            catch (COMException)
            {
                if (XamlApplicationLifetime.Exiting)
                {
                    return default;
                }

                throw;
            }

            currentXamlWindowReference.Window = window;
        }

        window.SwitchTo();
        window.AppWindow?.MoveInZOrderAtTop();
        return window;
    }

    private async ValueTask WaitForContentLoadedAsync()
    {
        if (currentXamlWindowReference.Window is not { Content: FrameworkElement content })
        {
            return;
        }

        if (content.IsLoaded)
        {
            return;
        }

        TaskCompletionSource tcs = new();
        RoutedEventHandler handler = (_, _) => tcs.TrySetResult();
        content.Loaded += handler;
        await tcs.Task.ConfigureAwait(true);
        content.Loaded -= handler;
    }
}
