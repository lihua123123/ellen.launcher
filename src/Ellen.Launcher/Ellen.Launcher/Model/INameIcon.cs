// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model;

public interface INameIcon<out TIcon>
{
    string Name { get; }

    TIcon Icon { get; }
}