// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;

namespace Ellen.Launcher.Service.Game;

[Service(ServiceLifetime.Singleton)]
public sealed partial class LaunchStatusOptions : ObservableObject
{
    [ObservableProperty]
    public partial UserGameRole? UserGameRole { get; set; }

    [ObservableProperty]
    public partial LaunchStatus? LaunchStatus { get; set; }
}