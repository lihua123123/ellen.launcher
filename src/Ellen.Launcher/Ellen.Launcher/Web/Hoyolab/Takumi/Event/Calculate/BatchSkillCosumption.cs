// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

public sealed class BatchSkillCosumption
{
    [JsonPropertyName("consume_list")]
    public List<Item> ConsumeList { get; set; } = default!;

    [JsonPropertyName("skill_info")]
    public PromotionDelta SkillInfo { get; set; } = default!;
}