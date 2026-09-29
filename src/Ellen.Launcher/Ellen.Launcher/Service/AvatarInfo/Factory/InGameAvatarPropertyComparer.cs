// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Collection.Generic;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

public sealed class InGameAvatarPropertyComparer : DelegatingPropertyComparer<AvatarProperty, FightProperty>
{
    private static readonly LazySlim<InGameAvatarPropertyComparer> LazyShared = new(() => new());

    private InGameAvatarPropertyComparer()
        : base(static a => a.FightProperty, InGameFightPropertyComparer.Shared)
    {
    }

    public static InGameAvatarPropertyComparer Shared { get => LazyShared.Value; }
}