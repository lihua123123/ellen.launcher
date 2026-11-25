// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Cultivation;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;
using System.Collections.Immutable;
using CalculableAvatar = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.Avatar;
using CalculableWeapon = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.Weapon;
using MetadataAvatar = Ellen.Launcher.Model.Metadata.Avatar.Avatar;
using MetadataWeapon = Ellen.Launcher.Model.Metadata.Weapon.Weapon;

namespace Ellen.Launcher.Service.Inventory;

[Service(ServiceLifetime.Singleton)]
public sealed partial class PromotionDeltaFactory
{
    private readonly ILogger<PromotionDeltaFactory> logger;
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial PromotionDeltaFactory(IServiceProvider serviceProvider);

    public async ValueTask<ImmutableArray<AvatarPromotionDelta>> GetAsync(ICultivationMetadataContext context, UserAndUid userAndUid)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        CalculateClient calculateClient = scope.ServiceProvider.GetRequiredService<CalculateClient>();
        ImmutableArray<CalculableAvatar> avatars = await calculateClient.GetAllAvatarsAsync(userAndUid).ConfigureAwait(false);
        if (avatars.IsDefault)
        {
            return default;
        }

        ImmutableArray<CalculableWeapon> weapons = await calculateClient.GetAllWeaponsAsync(userAndUid).ConfigureAwait(false);
        if (weapons.IsDefault)
        {
            return default;
        }

        // Do not cache an account- or metadata-dependent catalog for the entire process.
        ImmutableArray<ICultivationItemsAccess> entries = Create(context, avatars, weapons).Sort(CultivationItemsAccessComparer.Shared);
        ImmutableDictionary<uint, ElementAttributeId> elementAttrIds = CreateElementAttrIdMap(avatars);
        using (ValueStopwatch.MeasureExecution(logger))
        {
            return ToPromotionDeltaArray(entries, elementAttrIds);
        }
    }

    private static ImmutableDictionary<uint, ElementAttributeId> CreateElementAttrIdMap(ImmutableArray<CalculableAvatar> avatars)
    {
        ImmutableDictionary<uint, ElementAttributeId>.Builder builder = ImmutableDictionary.CreateBuilder<uint, ElementAttributeId>();
        foreach (ref readonly CalculableAvatar avatar in avatars.AsSpan())
        {
            builder[avatar.Id] = avatar.ElementAttrId;
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<ICultivationItemsAccess> Create(ICultivationMetadataContext context, ImmutableArray<CalculableAvatar> avatars, ImmutableArray<CalculableWeapon> weapons)
    {
        ImmutableArray<ICultivationItemsAccess>.Builder cultivationItems = ImmutableArray.CreateBuilder<ICultivationItemsAccess>(avatars.Length + weapons.Length);
        foreach (ref readonly CalculableAvatar item in avatars.AsSpan())
        {
            if (context.IdAvatarMap.TryGetValue(item.Id, out MetadataAvatar? avatar))
            {
                cultivationItems.Add(avatar);
            }
        }

        foreach (ref readonly CalculableWeapon item in weapons.AsSpan())
        {
            if (context.IdWeaponMap.TryGetValue(item.Id, out MetadataWeapon? weapon))
            {
                cultivationItems.Add(weapon);
            }
        }

        return cultivationItems.ToImmutable();
    }

    private static ImmutableArray<AvatarPromotionDelta> ToPromotionDeltaArray(ImmutableArray<ICultivationItemsAccess> cultivationItems, ImmutableDictionary<uint, ElementAttributeId> elementAttrIds)
    {
        List<AvatarPromotionDelta> deltas = [];
        int currentWeaponEmptyAvatarIndex = 0;

        foreach (ref readonly ICultivationItemsAccess item in cultivationItems.AsSpan())
        {
            switch (item)
            {
                case MetadataAvatar avatar:
                    AvatarPromotionDelta avatarDelta = AvatarPromotionDelta.CreateForAvatarMaxConsumption(avatar);

                    /* 官方计算器靠 element_attr_id 区分旅行者的元素形态：同一个 avatar_id 对应七种形态，
                       缺这个字段时服务端无法定位技能分组。7.2.0.7 探针里「e1 + 去掉 skill_list」的单条旅行者
                       返回 0，而只有 avatar_id 的条目返回 -500001。Remastered 不填该字段是因为它的元数据里
                       没有旅行者，这条 delta 根本不会出现。 */
                    if (elementAttrIds.TryGetValue(avatar.Id.Value, out ElementAttributeId elementAttrId))
                    {
                        avatarDelta.ElementAttrId = elementAttrId;
                    }

                    /* 本项目的元数据（ellen.wiki 产线）比 Remastered 多收了旅行者，且只带一种元素形态的技能分组
                       （10000005 是 830 / 832 / 839）。计算器只认账号当前形态的分组，带上就整批 -500001。
                       保留旅行者的等级、突破需求，只省略它的技能列表，代价是少算旅行者天赋书。 */
                    if (AvatarIds.IsPlayer(avatar.Id))
                    {
                        avatarDelta.SkillList = default;
                    }

                    deltas.Add(avatarDelta);

                    break;

                case MetadataWeapon weapon:
                    AvatarPromotionDelta delta;
                    if (currentWeaponEmptyAvatarIndex < deltas.Count)
                    {
                        delta = deltas[currentWeaponEmptyAvatarIndex++];
                    }
                    else
                    {
                        delta = new();
                        deltas.Add(delta);
                    }

                    delta.Weapon = new()
                    {
                        Id = weapon.Id,
                        LevelCurrent = 1,
                        LevelTarget = 90,
                    };

                    break;
            }
        }

        return [.. deltas];
    }

    private sealed class CultivationItemsAccessComparer : IComparer<ICultivationItemsAccess>
    {
        private static readonly LazySlim<CultivationItemsAccessComparer> LazyShared = new(() => new());

        private CultivationItemsAccessComparer()
        {
        }

        public static CultivationItemsAccessComparer Shared { get => LazyShared.Value; }

        public int Compare(ICultivationItemsAccess? x, ICultivationItemsAccess? y)
        {
            return (x, y) switch
            {
                (MetadataAvatar, MetadataWeapon) => -1,
                (MetadataWeapon, MetadataAvatar) => 1,
                _ => 0,
            };
        }
    }
}