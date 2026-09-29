// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Achievement;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class AchievementPage : ScopedPage
{
    public AchievementPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<AchievementViewModel>();
    }
}