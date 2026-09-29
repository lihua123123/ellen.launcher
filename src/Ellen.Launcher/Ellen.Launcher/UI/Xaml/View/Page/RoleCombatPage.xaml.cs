// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.RoleCombat;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class RoleCombatPage : ScopedPage
{
    public RoleCombatPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<RoleCombatViewModel>();
    }
}