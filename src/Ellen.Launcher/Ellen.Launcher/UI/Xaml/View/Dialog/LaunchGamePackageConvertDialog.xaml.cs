// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Service.Game.Package;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

[DependencyProperty<PackageConvertStatus>("State")]
public sealed partial class LaunchGamePackageConvertDialog : ContentDialog
{
    public LaunchGamePackageConvertDialog(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        DataContext = this;
    }
}
