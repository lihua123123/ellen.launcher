// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Model.Calculable;

public interface ICalculable : INameIcon<Uri>
{
    QualityType Quality { get; }

    uint LevelCurrent { get; set; }

    uint LevelTarget { get; set; }
}