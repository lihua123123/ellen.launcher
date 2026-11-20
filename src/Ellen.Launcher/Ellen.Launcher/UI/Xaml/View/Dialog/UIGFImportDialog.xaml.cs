// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.InterChange.GachaLog;
using Ellen.Launcher.Model.Intrinsic;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using System.Collections.Immutable;
using WinRT;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

[DependencyProperty<UIGF4>("UIGF")]
[DependencyProperty<ImmutableArray<UIGFUidSelection>>("ItemsSource", NotNull = true)]
public sealed partial class UIGFImportDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;
    private ImmutableArray<(GachaGameType Game, ulong Uid)> selectedAccounts = [];

    public UIGFImportDialog(IServiceProvider serviceProvider, UIGF4 uigf, ImmutableArray<UIGFUidSelection> accounts)
    {
        InitializeComponent();
        contentDialogFactory = serviceProvider.GetRequiredService<IContentDialogFactory>();
        UIGF = uigf;
        ItemsSource = accounts;
        IsPrimaryButtonEnabled = false;
    }

    public async ValueTask<ValueResult<bool, HashSet<(GachaGameType Game, ulong Uid)>>> GetSelectedAccountsAsync()
    {
        if (await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false) is ContentDialogResult.Primary)
        {
            await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
            return new(true, selectedAccounts.ToHashSet());
        }

        return new(false, []);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedAccounts = UIGFUidSelection.GetSelectedAccounts(sender.As<ListViewBase>());
        IsPrimaryButtonEnabled = selectedAccounts.Length > 0;
    }
}
