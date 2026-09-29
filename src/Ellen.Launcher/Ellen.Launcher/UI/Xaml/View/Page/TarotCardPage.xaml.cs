// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.RoleCombat;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class TarotCardPage : ScopedPage
{
    public TarotCardPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<TarotCardViewModel>();
    }
}
