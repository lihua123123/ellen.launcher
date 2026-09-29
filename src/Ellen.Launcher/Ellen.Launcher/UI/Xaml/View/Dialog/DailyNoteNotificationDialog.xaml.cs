// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Model.Entity;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

public sealed partial class DailyNoteNotificationDialog : ContentDialog
{
    public DailyNoteNotificationDialog(IServiceProvider serviceProvider, DailyNoteEntry entry)
    {
        InitializeComponent();

        DataContext = entry;
    }
}
