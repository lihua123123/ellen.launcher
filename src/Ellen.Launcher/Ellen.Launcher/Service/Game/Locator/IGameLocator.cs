// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.Locator;

public interface IGameLocator
{
    ValueTask<ValueResult<bool, string>> LocateSingleGamePathAsync();
}