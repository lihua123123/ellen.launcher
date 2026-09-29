// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model;

public interface IDefaultIdentity<out TIdentity>
{
    TIdentity Id { get; }
}