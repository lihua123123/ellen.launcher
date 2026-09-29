// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class GachaLogPage : ScopedPage
{
    public GachaLogPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<GachaLogViewModel>();
    }
}