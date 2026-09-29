// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class ProfilePicture
{
    public required ProfilePictureId Id { get; init; }

    public required string Icon { get; init; }

    public required string Name { get; init; }

    public required ProfilePictureUnlockBy UnlockType { get; init; }

    /// <summary>
    /// <see cref="ProfilePictureUnlockBy.Item"/> -> <see cref="MaterialId"/>
    /// <br/>
    /// <see cref="ProfilePictureUnlockBy.Avatar"/> -> <see cref="AvatarId"/>
    /// <br/>
    /// <see cref="ProfilePictureUnlockBy.Costume"/> -> <see cref="CostumeId"/>
    /// <br/>
    /// <see cref="ProfilePictureUnlockBy.ParentQuest"/> -> <see cref="QuestId"/>
    /// </summary>
    public uint UnlockParameter { get; init; }
}
