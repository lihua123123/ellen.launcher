// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;

namespace Ellen.Launcher.ViewModel.HardChallenge;

public sealed class AvatarBling : AvatarView
{
    private AvatarBling(bool isPlus, Avatar metaAvatar)
        : base(metaAvatar)
    {
        IsPlus = isPlus;
    }

    public bool IsPlus { get; }

    public static AvatarBling Create(HardChallengeBlingAvatar avatar, HardChallengeMetadataContext context)
    {
        return new(avatar.IsPlus, context.GetAvatar(avatar.AvatarId));
    }

    public static AvatarBling Create(HardChallengeBlingAvatar avatar, Avatar metaAvatar)
    {
        return new(avatar.IsPlus, metaAvatar);
    }
}