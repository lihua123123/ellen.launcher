// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Enka.Model;

public sealed class ShowAvatarInfo
{
    [JsonPropertyName("avatarId")]
    public AvatarId AvatarId { get; set; }

    [JsonPropertyName("level")]
    public Level Level { get; set; }

    [JsonPropertyName("energyType")]
    public ElementType EnergyType { get; set; }

    [JsonPropertyName("costumeId")]
    public CostumeId? CostumeId { get; set; }
}