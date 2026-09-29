// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.SpiralAbyss;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class SpiralAbyssRecordPage : ScopedPage
{
    public SpiralAbyssRecordPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<SpiralAbyssViewModel>();
    }
}