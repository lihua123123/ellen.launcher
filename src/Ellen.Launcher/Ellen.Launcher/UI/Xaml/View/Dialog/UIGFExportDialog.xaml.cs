// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.Intrinsic;
using System.Collections.Immutable;
using WinRT;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

[DependencyProperty<IReadOnlyList<UIGFUidSelection>>("ItemsSource")]
public sealed partial class UIGFExportDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;
    private ImmutableArray<(GachaGameType Game, ulong Uid)> selectedAccounts = [];

    public UIGFExportDialog(IServiceProvider serviceProvider, ImmutableArray<UIGFUidSelection> accounts, bool isLegacyVersion)
    {
        InitializeComponent();
        contentDialogFactory = serviceProvider.GetRequiredService<IContentDialogFactory>();

        if (isLegacyVersion)
        {
            SelectionListView.SelectionMode = ListViewSelectionMode.Single;
        }

        ItemsSource = accounts;
        IsPrimaryButtonEnabled = false;
    }

    public async ValueTask<ValueResult<bool, ImmutableArray<(GachaGameType Game, ulong Uid)>>> GetSelectedAccountsAsync()
    {
        bool confirmed = await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false) is ContentDialogResult.Primary;
        await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
        return new(confirmed, selectedAccounts);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedAccounts = UIGFUidSelection.GetSelectedAccounts(sender.As<ListViewBase>());
        IsPrimaryButtonEnabled = selectedAccounts.Length > 0;
    }
}
