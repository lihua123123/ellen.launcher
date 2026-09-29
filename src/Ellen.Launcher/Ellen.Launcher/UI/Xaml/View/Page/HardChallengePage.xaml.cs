// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.HardChallenge;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class HardChallengePage : ScopedPage
{
    public HardChallengePage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<HardChallengeViewModel>();
    }
}