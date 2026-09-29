// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.ViewModel.Abstraction;

namespace Ellen.Launcher.UI.Xaml.View.Card;

public sealed partial class SignInCard : Button
{
    public SignInCard(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        this.InitializeViewModelSlim<ViewModel.SignIn.SignInViewModelSlim>(serviceProvider);
    }
}
