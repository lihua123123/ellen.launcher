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
    private ImmutableArray<uint> selectedUids = [];

    public UIGFImportDialog(IServiceProvider serviceProvider, UIGF4 uigf)
    {
        InitializeComponent();
        contentDialogFactory = serviceProvider.GetRequiredService<IContentDialogFactory>();

        UIGF = uigf;

        ImmutableArray<UIGFUidSelection> genshin = uigf.Hk4e
            .SelectAsArray(static item => UIGFUidSelection.Create(item.Uid, GachaGameType.Genshin));

        // 绝区零调频记录在 UIGF v4.2 中以 nap 字段导出
        ImmutableArray<UIGFUidSelection> zzz = uigf is UIGF42 uigf42
            ? uigf42.Nap.SelectAsArray(static item => UIGFUidSelection.Create(item.Uid, GachaGameType.ZZZ))
            : [];

        ItemsSource = [.. genshin, .. zzz];
    }

    public async ValueTask<ValueResult<bool, HashSet<uint>>> GetSelectedUidsAsync()
    {
        if (await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false) is ContentDialogResult.Primary)
        {
            await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
            return new(true, selectedUids.ToHashSet());
        }

        return new(false, default!);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        selectedUids = UIGFUidSelection.GetSelectedUidArray(sender.As<ListViewBase>());
    }
}
