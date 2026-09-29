// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Navigation;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.SpiralAbyss;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.UI.Xaml.View.Dialog;
using Ellen.Launcher.UI.Xaml.View.Page;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Upstream.Response;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.ViewModel.SpiralAbyss;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class SpiralAbyssViewModel : Abstraction.ViewModel, IRecipient<UserAndUidChangedMessage>
{
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly ISpiralAbyssService spiralAbyssService;
    private readonly INavigationService navigationService;
    private readonly IServiceProvider serviceProvider;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IUserService userService;
    private readonly IMessenger messenger;

    private SpiralAbyssMetadataContext? metadataContext;

    [GeneratedConstructor]
    public partial SpiralAbyssViewModel(IServiceProvider serviceProvider);

    public IAdvancedCollectionView<SpiralAbyssView>? SpiralAbyssEntries
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentSpiralAbyssEntryChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(field, OnCurrentSpiralAbyssEntryChanged);
        }
    }

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is { } userAndUid)
        {
            UpdateSpiralAbyssCollectionAsync(userAndUid).SafeForget();
        }
        else
        {
            SpiralAbyssEntries?.MoveCurrentTo(default);
        }
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return false;
        }

        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
        {
            metadataContext = await metadataService.GetContextAsync<SpiralAbyssMetadataContext>(token).ConfigureAwait(false);
            await UpdateSpiralAbyssCollectionAsync(userAndUid).ConfigureAwait(false);
        }
        else
        {
            messenger.Send(InfoBarMessage.Warning(SH.MustSelectUserAndUid));
        }

        return true;
    }

    private void OnCurrentSpiralAbyssEntryChanged(object? sender, object? e)
    {
        SpiralAbyssEntries?.CurrentItem?.Floors.MoveCurrentToFirst();
    }

    [SuppressMessage("", "SH003")]
    private async Task UpdateSpiralAbyssCollectionAsync(UserAndUid userAndUid)
    {
        if (metadataContext is null)
        {
            return;
        }

        try
        {
            ObservableCollection<SpiralAbyssView> collection;
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                collection = await spiralAbyssService
                    .GetSpiralAbyssViewCollectionAsync(metadataContext, userAndUid)
                    .ConfigureAwait(false);
            }

            IAdvancedCollectionView<SpiralAbyssView> spiralAbyssEntries = collection.AsAdvancedCollectionView();

            await taskContext.SwitchToMainThreadAsync();
            SpiralAbyssEntries = spiralAbyssEntries;
            SpiralAbyssEntries.MoveCurrentTo(SpiralAbyssEntries.Source.FirstOrDefault(s => s.Engaged));
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Command("RefreshCommand")]
    private async Task RefreshAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Refresh spiral abyss record", "SpiralAbyssRecordViewModel.Command"));

        if (metadataContext is null)
        {
            return;
        }

        if (SpiralAbyssEntries is not null)
        {
            if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
            {
                try
                {
                    using (await EnterCriticalSectionAsync().ConfigureAwait(false))
                    {
                        await spiralAbyssService
                            .RefreshSpiralAbyssAsync(metadataContext, userAndUid)
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }

                await taskContext.SwitchToMainThreadAsync();
                SpiralAbyssEntries.MoveCurrentTo(SpiralAbyssEntries.Source.FirstOrDefault(s => s.Engaged));
            }
        }
    }

}
