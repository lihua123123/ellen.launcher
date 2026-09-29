// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.ViewModel.SpiralAbyss;

public sealed class RankAvatar : AvatarView
{
    public RankAvatar(int value, Model.Metadata.Avatar.Avatar avatar)
        : base(avatar)
    {
        Value = value;
    }

    public int Value { get; }
}