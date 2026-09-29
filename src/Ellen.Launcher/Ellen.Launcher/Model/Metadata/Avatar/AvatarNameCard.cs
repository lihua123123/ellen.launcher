// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class AvatarNameCard : NameDescription
{
    public required string Icon { get; init; }

    public required string PicturePrefix { get; init; }
}