// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Calculable;

public interface ICalculableMinMaxLevel : ICalculable
{
    uint LevelMin { get; }

    uint LevelMax { get; }
}