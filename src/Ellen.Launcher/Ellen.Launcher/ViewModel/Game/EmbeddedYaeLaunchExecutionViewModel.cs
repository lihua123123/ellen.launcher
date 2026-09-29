// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Property;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Game.Package;
using Ellen.Launcher.Service.Game.Scheme;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.UI.Xaml.View.Dialog;

namespace Ellen.Launcher.ViewModel.Game;

[Service(ServiceLifetime.Transient)]
public sealed partial class EmbeddedYaeLaunchExecutionViewModel : IViewModelSupportLaunchExecution
{
    private readonly IServiceProvider serviceProvider;
    private readonly LaunchOptions launchOptions;
    private readonly IGameService gameService;
    private readonly ITaskContext taskContext;
    private readonly LaunchGameShared shared;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial EmbeddedYaeLaunchExecutionViewModel(IServiceProvider serviceProvider);

    public LaunchSchemeFilteredGameAccountsView CurrentSchemeFilteredGameAccountsView { get => field ??= new(Property.Create(false), gameService, taskContext, messenger); }

    LaunchScheme? IViewModelSupportLaunchExecution.TargetScheme { get => CurrentSchemeFilteredGameAccountsView.Scheme; }

    LaunchScheme? IViewModelSupportLaunchExecution.CurrentScheme { get => CurrentSchemeFilteredGameAccountsView.Scheme; }

    GameAccount? IViewModelSupportLaunchExecution.GameAccount { get => CurrentSchemeFilteredGameAccountsView.View?.CurrentItem; }

    ValueTask<BlockDeferral<PackageConvertStatus>> IViewModelSupportLaunchExecution.CreateConvertBlockDeferralAsync()
    {
        return BlockDeferral<PackageConvertStatus>.CreateAsync<LaunchGamePackageConvertDialog>(serviceProvider, static (state, dialog) => dialog.State = state);
    }

    public async ValueTask<bool> InitializeAsync()
    {
        try
        {
            LaunchScheme? currentScheme = launchOptions.GamePathEntry.Value is not null
                ? shared.GetCurrentLaunchSchemeFromConfigurationFile()
                : default;

            await taskContext.SwitchToMainThreadAsync();
            await CurrentSchemeFilteredGameAccountsView.SetAsync(currentScheme).ConfigureAwait(true);
            return true;
        }
        catch (Exception ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
            return false;
        }
    }
}