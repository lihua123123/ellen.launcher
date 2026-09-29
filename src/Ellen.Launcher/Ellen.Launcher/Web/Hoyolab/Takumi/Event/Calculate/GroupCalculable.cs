// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

public abstract class GroupCalculable : Calculable
{
    [JsonPropertyName("group_id")]
    public uint GroupId { get; set; }

    public override PromotionDelta ToPromotionDelta()
    {
        return new()
        {
            LevelCurrent = LevelCurrent,
            LevelTarget = LevelTarget,
            Id = GroupId,
        };
    }
}
