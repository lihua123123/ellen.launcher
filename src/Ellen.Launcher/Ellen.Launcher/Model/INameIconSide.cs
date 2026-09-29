// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model;

public interface INameIconSide<out TIcon> : INameIcon<TIcon>
{
    TIcon SideIcon { get; }
}