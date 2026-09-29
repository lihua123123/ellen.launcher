// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Enka.Model;

public sealed class ProfilePicture
{
    [JsonPropertyName("id")]
    public ProfilePictureId Id { get; set; }

    [JsonPropertyName("avatarId")]
    public AvatarId AvatarId { get; set; }

    [JsonPropertyName("costumeId")]
    public CostumeId CostumeId { get; set; }
}