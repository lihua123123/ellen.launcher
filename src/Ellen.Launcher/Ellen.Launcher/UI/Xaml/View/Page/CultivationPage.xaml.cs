// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Cultivation;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class CultivationPage : ScopedPage
{
    public CultivationPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<CultivationViewModel>();
    }
}