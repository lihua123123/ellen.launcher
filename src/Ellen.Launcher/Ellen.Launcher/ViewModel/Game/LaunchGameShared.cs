// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
// Copyright (c) Millennium-Science-Technology-R-D-Inst. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Game.Configuration;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Launching;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Game.Launching.Invoker;
using Ellen.Launcher.Service.Game.Scheme;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.UI.Xaml.View.Page;
using Ellen.Launcher.ViewModel.User;
using System.IO;

namespace Ellen.Launcher.ViewModel.Game;

[Service(ServiceLifetime.Singleton)]
public sealed partial class LaunchGameShared
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly LaunchOptions launchOptions;
    private readonly ITaskContext taskContext;
    private readonly IGameService gameService;
    private readonly IMessenger messenger;

    private bool resuming;

    [GeneratedConstructor]
    public partial LaunchGameShared(IServiceProvider serviceProvoder);

    public LaunchScheme? GetCurrentLaunchSchemeFromConfigurationFile(bool showInfo = true)
    {
        ChannelOptions options = gameService.GetChannelOptions();

        if (options.ErrorKind is ChannelOptionsErrorKind.None)
        {
            try
            {
                return KnownLaunchSchemes.Values.Single(scheme => scheme.Equals(options));
            }
            catch (InvalidOperationException)
            {
                // 后台收集
                EllenException.Throw($"不支持的 ChannelOptions: {options}");
            }
        }

        if (!showInfo)
        {
            return default;
        }

        InfoBarMessage? message = options.ErrorKind switch
        {
            ChannelOptionsErrorKind.ConfigurationFileNotFound => InfoBarMessage.Warning(
                SH.FormatViewModelLaunchGameConfigurationFailed(options.ErrorKind),
                SH.FormatViewModelLaunchGameConfigurationFileNotFound(options.FilePath)),
            ChannelOptionsErrorKind.GamePathNullOrEmpty => InfoBarMessage.Warning(
                SH.FormatViewModelLaunchGameConfigurationFailed(options.ErrorKind),
                SH.ViewModelLaunchGameGamePathNullOrEmpty),
            ChannelOptionsErrorKind.DeviceNotFound => InfoBarMessage.Warning(
                SH.FormatViewModelLaunchGameConfigurationFailed(options.ErrorKind),
                SH.ViewModelLaunchGameDeviceNotFound),
            ChannelOptionsErrorKind.GameContentCorrupted => InfoBarMessage.Warning(
                SH.FormatViewModelLaunchGameConfigurationFailed(options.ErrorKind),
                SH.FormatViewModelLaunchGameContentCorrupted(options.FilePath)),
            ChannelOptionsErrorKind.GamePathLocked => InfoBarMessage.Error(
                SH.ViewModelGameConfigurationCreateFailedGamePathLocked,
                options.FilePath ?? string.Empty),
            _ => default,
        };

        if (message is not null)
        {
            messenger.Send(message);
        }

        return default;
    }

    public async ValueTask DefaultLaunchExecutionAsync(IViewModelSupportLaunchExecution viewModel, UserAndUid? userAndUid)
    {
        // The game process can exist longer than the view model
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            DefaultLaunchExecutionInvoker invoker = new();
            try
            {
                LaunchExecutionInvocationContext context = new()
                {
                    ViewModel = viewModel,
                    ServiceProvider = scope.ServiceProvider,
                    LaunchOptions = scope.ServiceProvider.GetRequiredService<LaunchOptions>(),
                    Identity = GameIdentity.Create(userAndUid, viewModel.GameAccount),
                };

                await invoker.InvokeAsync(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                scope.ServiceProvider.GetRequiredService<IMessenger>().Send(InfoBarMessage.Error(ex));
            }
        }
    }

    public async ValueTask ResumeLaunchExecutionAsync(IViewModelSupportLaunchExecution viewModel)
    {
        if (Interlocked.Exchange(ref resuming, true))
        {
            return;
        }

        try
        {
            if (!await GameLifeCycle.IsGameRunningAsync(taskContext).ConfigureAwait(false))
            {
                return;
            }

            if (AbstractLaunchExecutionInvoker.Invoking())
            {
                return;
            }

            if (GetCurrentLaunchSchemeFromConfigurationFile(false) is null)
            {
                return;
            }

            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                try
                {
                    LaunchExecutionInvocationContext context = new()
                    {
                        ViewModel = viewModel,
                        ServiceProvider = scope.ServiceProvider,
                        LaunchOptions = launchOptions,
                        Identity = GameIdentity.Create(),
                    };

                    await new ResumeLaunchExecutionInvoker().InvokeAsync(context).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    messenger.Send(InfoBarMessage.Error(ex));
                }
            }
        }
        finally
        {
            Volatile.Write(ref resuming, false);
        }
    }

    public async ValueTask ConvertLaunchExecutionAsync(IViewModelSupportLaunchExecution viewModel)
    {
        ConvertOnlyLaunchExecutionInvoker invoker = new();
        try
        {
            using (IServiceScope scope = serviceProvider.CreateScope())
            {
                LaunchExecutionInvocationContext context = new()
                {
                    ViewModel = viewModel,
                    ServiceProvider = scope.ServiceProvider,
                    LaunchOptions = launchOptions,
                    Identity = GameIdentity.Create(),
                };

                await invoker.InvokeAsync(context).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }
    }
}