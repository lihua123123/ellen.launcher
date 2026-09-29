// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Media.Animation;

namespace Ellen.Launcher.Service.Navigation;

public interface ISupportNavigationTransitionInfo
{
    NavigationTransitionInfo? TransitionInfo { get; }
}