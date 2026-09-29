// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;

public sealed class Skill
{
    [JsonPropertyName("skill_id")]
    public SkillId SkillId { get; set; }

    [JsonPropertyName("skill_type")]
    public SkillType SkillType { get; set; }

    [JsonPropertyName("level")]
    public SkillLevel Level { get; set; }

    // Ignored field string desc
    // Ignored field * skill_affix_list
}