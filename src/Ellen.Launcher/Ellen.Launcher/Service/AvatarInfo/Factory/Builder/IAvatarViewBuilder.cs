// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Abstraction;
using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory.Builder;

public interface IAvatarViewBuilder : IBuilder
{
    AvatarView View { get; }
}