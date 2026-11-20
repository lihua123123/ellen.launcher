// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Home;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class AnnouncementPage : ScopedPage
{
    public AnnouncementPage()
    {
        InitializeComponent();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<AnnouncementViewModel>();
    }

    private void RedeemFlyout_Opened(object sender, object e)
    {
        if (DataContext is AnnouncementViewModel viewModel)
        {
            viewModel.MarkLiveRedeemCodesViewed();
        }
    }
}