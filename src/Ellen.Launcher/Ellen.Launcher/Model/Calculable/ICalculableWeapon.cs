// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Calculable;

public interface ICalculableWeapon : ICalculablePromoteLevel
{
    WeaponId WeaponId { get; }
}