// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.DailyNote;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class DailyNotePage : ScopedPage
{
    public DailyNotePage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<DailyNoteViewModel>();
    }
}