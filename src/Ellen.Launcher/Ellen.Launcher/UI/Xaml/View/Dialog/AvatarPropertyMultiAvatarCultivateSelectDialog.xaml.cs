// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.ViewModel.AvatarProperty;
using System.Collections.Immutable;
using WinRT;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

[DependencyProperty<IAdvancedCollectionView<AvatarView>>("Avatars")]
public sealed partial class AvatarPropertyMultiAvatarCultivateSelectDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;

    [GeneratedConstructor(InitializeComponent = true)]
    public partial AvatarPropertyMultiAvatarCultivateSelectDialog(IServiceProvider serviceProvider);

    public ImmutableArray<AvatarView> SelectedAvatars { get; private set; } = [];

    public async ValueTask<bool> SelectAvatarsAsync()
    {
        return await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false) is ContentDialogResult.Primary;
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedAvatars = [.. sender.As<ListViewBase>().SelectedItems.Cast<AvatarView>()];
    }
}