// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.Property;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

[BindableCustomPropertyProvider]
public sealed partial class AvatarPromotionDelta : ObservableObject
{
    [ObservableProperty]
    [JsonPropertyName("avatar_id")]
    public partial AvatarId AvatarId { get; set; }

    [ObservableProperty]
    [JsonPropertyName("avatar_level_current")]
    public partial uint AvatarLevelCurrent { get; set; }

    [ObservableProperty]
    [JsonPropertyName("avatar_level_target")]
    public partial uint AvatarLevelTarget { get; set; }

    [ObservableProperty]
    [JsonPropertyName("element_attr_id")]
    public partial ElementAttributeId ElementAttrId { get; set; }

    [ObservableProperty]
    [JsonPropertyName("skill_list")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public partial ImmutableArray<PromotionDelta> SkillList { get; set; }

    [ObservableProperty]
    [JsonPropertyName("weapon")]
    public partial PromotionDelta? Weapon { get; set; }

    /* 服务端据此判断「这份 delta 来自玩家的真实账号」，只有 true 时才会按 uid + region
       去读玩家的背包并回填 overall_consume 的 num / lack_num。
       曾经把它设成 false 并加 WhenWritingDefault，字段根本不会出现在请求体里，
       服务端于是按纯本地计算处理 —— 表现就是「同步成功但背包全是 0」。
       与 Snap.Hutao.Remastered 保持一致：默认 true，且始终序列化。 */
    [ObservableProperty]
    [JsonPropertyName("from_user_sync")]
    public partial bool FromUserSync { get; set; } = true;

    [ObservableProperty]
    [JsonPropertyName("avatar_promote_level")]
    public partial uint AvatarPromoteLevel { get; set; }

    public IProperty<bool> IsViewUnloaded { get; } = Property.Create(false);

    public static AvatarPromotionDelta CreateForBaseline()
    {
        return new()
        {
            AvatarLevelTarget = LocalSetting.Get(SettingKeys.CultivationAvatarLevelTarget, 90U),
            SkillList =
            [
                new() { LevelTarget = LocalSetting.Get(SettingKeys.CultivationAvatarSkillATarget, 10U), },
                new() { LevelTarget = LocalSetting.Get(SettingKeys.CultivationAvatarSkillETarget, 10U), },
                new() { LevelTarget = LocalSetting.Get(SettingKeys.CultivationAvatarSkillQTarget, 10U), },
            ],
            Weapon = new() { LevelTarget = LocalSetting.Get(SettingKeys.CultivationWeapon90LevelTarget, 90U), },
        };
    }

    public static AvatarPromotionDelta CreateForAvatarMaxConsumption(Model.Metadata.Avatar.Avatar avatar)
    {
        return new()
        {
            AvatarId = avatar.Id,
            AvatarLevelCurrent = 1,
            AvatarLevelTarget = 90,
            SkillList = avatar.SkillDepot.CompositeSkillsNoInherents.SelectAsArray(static skill => new PromotionDelta
            {
                Id = skill.GroupId,
                LevelCurrent = 1,
                LevelTarget = 10,
            }),
        };
    }
}