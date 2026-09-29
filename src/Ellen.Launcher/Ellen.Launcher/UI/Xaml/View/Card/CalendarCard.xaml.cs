// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.ViewModel.Abstraction;

namespace Ellen.Launcher.UI.Xaml.View.Card;

public sealed partial class CalendarCard : Button
{
    public CalendarCard(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        this.InitializeViewModelSlim<ViewModel.Calendar.CalendarViewModel>(serviceProvider);
    }
}