// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;

/// <summary>
/// 绝区零战绩「代理人简略信息」。
/// </summary>
public class ZZZAvatarBasic
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("element_type")]
    public int ElementType { get; set; }

    [JsonPropertyName("avatar_profession")]
    public int Profession { get; set; }

    [JsonPropertyName("rank")]
    public int Rank { get; set; }

    [JsonPropertyName("role_square_url")]
    public string RoleSquareUrl { get; set; } = string.Empty;

    [JsonPropertyName("name_mi18n")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("full_name_mi18n")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("camp_name_mi18n")]
    public string CampName { get; set; } = string.Empty;

    [JsonPropertyName("group_icon_path")]
    public string GroupIconPath { get; set; } = string.Empty;

    [JsonPropertyName("hollow_icon_path")]
    public string HollowIconPath { get; set; } = string.Empty;
}

/// <summary>
/// 绝区零战绩「代理人详情」。
/// </summary>
public sealed class ZZZFullAgent : ZZZAvatarBasic
{
    [JsonPropertyName("properties")]
    public List<ZZZProperty> Properties { get; set; } = [];

    [JsonPropertyName("equip")]
    public List<ZZZDisc> Discs { get; set; } = [];

    [JsonPropertyName("weapon")]
    public ZZZWeapon? Weapon { get; set; }

    [JsonPropertyName("skills")]
    public List<ZZZSkill> Skills { get; set; } = [];

    [JsonPropertyName("ranks")]
    public List<ZZZRank> Ranks { get; set; } = [];

    [JsonPropertyName("skill_awaken")]
    public ZZZAwakenedPotentialInfo? AwakenedPotential { get; set; }

    [JsonPropertyName("role_vertical_painting_url")]
    public string BannerIcon { get; set; } = string.Empty;
}

public sealed class ZZZAvatarListWrapper<T>
{
    [JsonPropertyName("avatar_list")]
    public List<T> List { get; set; } = [];
}

public sealed class ZZZProperty
{
    [JsonPropertyName("property_name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("property_id")]
    public int PropertyId { get; set; }

    [JsonPropertyName("base")]
    public JsonElement Base { get; set; }

    [JsonPropertyName("add")]
    public JsonElement Add { get; set; }

    [JsonPropertyName("final")]
    public JsonElement Final { get; set; }
}

public sealed class ZZZDisc
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("main_properties")]
    public List<ZZZProperty> MainProperties { get; set; } = [];

    [JsonPropertyName("properties")]
    public List<ZZZProperty> Properties { get; set; } = [];

    [JsonPropertyName("equip_suit")]
    public ZZZDiscSetEffect? SetEffect { get; set; }

    [JsonPropertyName("equipment_type")]
    public int EquipmentType { get; set; }
}

public sealed class ZZZDiscSetEffect
{
    [JsonPropertyName("suit_id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("own")]
    public int OwnedCount { get; set; }

    [JsonPropertyName("desc1")]
    public string TwoPieceDescription { get; set; } = string.Empty;

    [JsonPropertyName("desc2")]
    public string FourPieceDescription { get; set; } = string.Empty;
}

public sealed class ZZZWeapon
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("star")]
    public int Star { get; set; }

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public List<ZZZProperty> Properties { get; set; } = [];

    [JsonPropertyName("main_properties")]
    public List<ZZZProperty> MainProperties { get; set; } = [];

    [JsonPropertyName("talent_title")]
    public string TalentTitle { get; set; } = string.Empty;

    [JsonPropertyName("talent_content")]
    public string TalentContent { get; set; } = string.Empty;

    [JsonPropertyName("profession")]
    public int Profession { get; set; }
}

public sealed class ZZZSkill
{
    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("skill_type")]
    public int SkillType { get; set; }

    [JsonPropertyName("items")]
    public List<ZZZSkillItem> Items { get; set; } = [];
}

public sealed class ZZZSkillItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class ZZZRank
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("pos")]
    public int Position { get; set; }

    [JsonPropertyName("is_unlocked")]
    public bool IsUnlocked { get; set; }
}

public sealed class ZZZAwakenedPotentialInfo
{
    [JsonPropertyName("has_awaken_system")]
    public bool HasAwakenSystem { get; set; }

    [JsonPropertyName("awaken_level")]
    public int CurrentLevel { get; set; }

    [JsonPropertyName("awaken_max_level")]
    public int MaxLevel { get; set; }

    [JsonPropertyName("skill_awaken_items")]
    public List<ZZZAwakenedPotential> Items { get; set; } = [];
}

public sealed class ZZZAwakenedPotential
{
    [JsonPropertyName("awaken_level")]
    public int Level { get; set; }

    [JsonPropertyName("level_show_name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("awaken_skill_items")]
    public List<ZZZAwakenedPotentialSkill> Skills { get; set; } = [];
}

public sealed class ZZZAwakenedPotentialSkill
{
    [JsonPropertyName("skill_type")]
    public int SkillType { get; set; }

    [JsonPropertyName("awaken_simple_info")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("skill_items")]
    public List<ZZZAwakenedPotentialSkillDetail> Details { get; set; } = [];
}

public sealed class ZZZAwakenedPotentialSkillDetail
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
