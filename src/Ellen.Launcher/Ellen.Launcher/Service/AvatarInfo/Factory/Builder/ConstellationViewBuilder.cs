// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory.Builder;

public sealed class ConstellationViewBuilder : IConstellationViewBuilder
{
    public ConstellationView View { get; } = new();
}