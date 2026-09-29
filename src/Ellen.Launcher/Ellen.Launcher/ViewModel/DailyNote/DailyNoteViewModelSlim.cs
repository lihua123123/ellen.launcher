// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.DailyNote;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.UI.Xaml.View.Page;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.ViewModel.DailyNote;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
public sealed partial class DailyNoteViewModelSlim : Abstraction.ViewModelSlim<DailyNotePage>
{
    private readonly IDailyNoteService dailyNoteService;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    private DailyNoteMetadataContext? metadataContext;

    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial DailyNoteViewModelSlim(IServiceProvider serviceProvider);

    // This property must be a reference type
    [ObservableProperty]
    public partial List<DailyNoteEntry>? DailyNoteEntries { get; set; }

    /// <inheritdoc/>
    protected override async Task LoadAsync()
    {
        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return;
        }

        metadataContext = await metadataService.GetContextAsync<DailyNoteMetadataContext>().ConfigureAwait(false);

        try
        {
            await taskContext.SwitchToBackgroundAsync();
            ObservableCollection<DailyNoteEntry> entries = await dailyNoteService
                .GetDailyNoteEntryCollectionAsync(metadataContext)
                .ConfigureAwait(false);

            await taskContext.SwitchToMainThreadAsync();

            // We must make a copy of the entries collection to avoid the following exception:
            // Element is already the child of another element.
            DailyNoteEntries = [.. entries];
            IsInitialized = true;
        }
        catch (EllenException ex)
        {
            messenger.Send(InfoBarMessage.Error(ex));
        }
    }
}