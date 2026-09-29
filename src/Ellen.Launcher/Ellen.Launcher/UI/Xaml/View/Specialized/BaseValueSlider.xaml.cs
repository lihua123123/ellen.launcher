// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.ViewModel.Wiki;

namespace Ellen.Launcher.UI.Xaml.View.Specialized;

[DependencyProperty<BaseValueInfo>("BaseValueInfo")]
[DependencyProperty<bool>("IsPromoteVisible", DefaultValue = true, NotNull = true)]
public sealed partial class BaseValueSlider : UserControl
{
    public BaseValueSlider()
    {
        InitializeComponent();
    }
}