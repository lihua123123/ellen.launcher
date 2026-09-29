// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Calculable;

public interface ICalculableAvatar : ICalculablePromoteLevel
{
    AvatarId AvatarId { get; }

    ImmutableArray<ICalculableSkill> Skills { get; }
}