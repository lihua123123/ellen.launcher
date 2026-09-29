// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Model.InterChange.Achievement;
using Ellen.Launcher.Service.Achievement;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

[DependencyProperty<UIAF>("UIAF")]
public sealed partial class AchievementImportDialog : ContentDialog
{
    private readonly IContentDialogFactory contentDialogFactory;

    public AchievementImportDialog(IServiceProvider serviceProvider, UIAF uiaf)
    {
        InitializeComponent();

        contentDialogFactory = serviceProvider.GetRequiredService<IContentDialogFactory>();
        UIAF = uiaf;
    }

    public async ValueTask<ValueResult<bool, ImportStrategyKind>> GetImportStrategyAsync()
    {
        ContentDialogResult result = await contentDialogFactory.EnqueueAndShowAsync(this).ShowTask.ConfigureAwait(false);
        await contentDialogFactory.TaskContext.SwitchToMainThreadAsync();
        return new(result is ContentDialogResult.Primary, (ImportStrategyKind)ImportModeSelector.SelectedIndex);
    }
}