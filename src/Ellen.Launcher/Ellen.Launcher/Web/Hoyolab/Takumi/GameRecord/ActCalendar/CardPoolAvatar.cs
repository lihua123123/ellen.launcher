// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class CardPoolAvatar : CardPoolItem
{
    [JsonPropertyName("id")]
    public required AvatarId Id { get; set; }

    [JsonPropertyName("element")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public required ElementName Element { get; init; }

    [JsonPropertyName("is_invisible")]
    public required bool IsInvisible { get; set; }
}