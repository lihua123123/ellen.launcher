// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Wiki;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class WikiAvatarPage : ScopedPage
{
    public WikiAvatarPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<WikiAvatarViewModel>();
    }
}