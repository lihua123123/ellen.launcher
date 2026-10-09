// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.ViewModel.Abstraction;

namespace Ellen.Launcher.UI.Xaml.View.Card;

public sealed partial class TravelersDiaryCard : Button
{
    public TravelersDiaryCard(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        this.InitializeViewModelSlim<ViewModel.TravelersDiary.TravelersDiaryViewModelSlim>(serviceProvider);
    }
}
