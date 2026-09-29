// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory.Builder;

public sealed class ReliquaryViewBuilder : IReliquaryViewBuilder
{
    public ReliquaryView View { get; } = new();
}