// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Item;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.ViewModel.GachaLog;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;
using GachaItem = Ellen.Launcher.Model.Entity.GachaItem;

namespace Ellen.Launcher.Service.GachaLog.Factory;

[Service(ServiceLifetime.Singleton, typeof(IGachaStatisticsFactory))]
public sealed partial class GachaStatisticsFactory : IGachaStatisticsFactory
{
    // We must use the root service provider, if not, PullPrediction will cause memory leak
    private readonly IServiceProvider serviceProvider;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial GachaStatisticsFactory(IServiceProvider serviceProvider);

    /// <inheritdoc/>
    public async ValueTask<GachaStatistics> CreateAsync(GachaLogServiceMetadataContext metadata, ImmutableArray<GachaItem> items, ImmutableArray<BeyondGachaItem> beyondItems)
    {
        await taskContext.SwitchToBackgroundAsync();

        ImmutableArray<HistoryWishBuilder> historyWishBuilders = metadata.GachaEvents
            .SelectAsArray(static (gachaEvent, metadata) => HistoryWishBuilder.Create(gachaEvent, metadata), metadata);
        return SynchronizedCreate(new(serviceProvider, items, beyondItems, historyWishBuilders, metadata));
    }

    private static GachaStatistics SynchronizedCreate(GachaStatisticsFactoryContext context)
    {
        TypedWishSummaryBuilder standardWishBuilder = TypedWishSummaryBuilderContext.StandardWish(context).CreateBuilder();
        TypedWishSummaryBuilder avatarWishBuilder = TypedWishSummaryBuilderContext.AvatarEventWish(context).CreateBuilder();
        TypedWishSummaryBuilder weaponWishBuilder = TypedWishSummaryBuilderContext.WeaponEventWish(context).CreateBuilder();
        TypedWishSummaryBuilder chronicledWishBuilder = TypedWishSummaryBuilderContext.ChronicledWish(context).CreateBuilder();
        TypedWishSummaryBuilder noviceWishBuilder = TypedWishSummaryBuilderContext.NoviceWish(context).CreateBuilder();
        TypedWishSummaryBuilder beyondStandardWishBuilder = TypedWishSummaryBuilderContext.BeyondStandardWish(context).CreateBuilder();
        TypedWishSummaryBuilder beyondEventWishBuilder = TypedWishSummaryBuilderContext.BeyondEventWish(context).CreateBuilder();
        GachaStatisticsItemCounter itemCounter = new(context);

        // Pre group builders
        Dictionary<GachaType, List<HistoryWishBuilder>> historyWishBuilderMap = context.HistoryWishBuilders
            .GroupBy(b => b.ConfigType)
            .ToDictionary(g => g.Key, g => g.ToList().SortBy(b => b.From));

        // Items are ordered by precise time, first is oldest
        foreach (ref readonly GachaItem item in context.Items.AsSpan())
        {
            // Find target history wish to operate. // banner.From <= item.Time <= banner.To
            HistoryWishBuilder? targetHistoryWishBuilder = default;
            if (item.GachaType is not (GachaType.Standard or GachaType.NewBie) && historyWishBuilderMap.TryGetValue(item.GachaType, out List<HistoryWishBuilder>? builders))
            {
                targetHistoryWishBuilder = builders.BinarySearch(item, static (pinned, banner) => pinned.Time < banner.From ? -1 : pinned.Time > banner.To ? 1 : 0);
            }

            switch (item.ItemId.StringLength)
            {
                case 8U:
                    {
                        Avatar avatar = context.Metadata.GetAvatar(item.ItemId);

                        bool isUp = false;
                        switch (avatar.Quality)
                        {
                            case QualityType.QUALITY_ORANGE:
                                itemCounter.OrangeAvatar.IncreaseByOne(avatar);
                                isUp = targetHistoryWishBuilder?.IncreaseOrange(avatar) ?? false;
                                break;
                            case QualityType.QUALITY_PURPLE:
                                itemCounter.PurpleAvatar.IncreaseByOne(avatar);
                                targetHistoryWishBuilder?.IncreasePurple(avatar);
                                break;
                        }

                        standardWishBuilder.Track(item, avatar, isUp);
                        avatarWishBuilder.Track(item, avatar, isUp);
                        weaponWishBuilder.Track(item, avatar, isUp);
                        chronicledWishBuilder.Track(item, avatar, isUp);
                        noviceWishBuilder.Track(item, avatar, isUp);
                        break;
                    }

                case 5U:
                    {
                        Weapon weapon = context.Metadata.GetWeapon(item.ItemId);

                        bool isUp = false;
                        switch (weapon.RankLevel)
                        {
                            case QualityType.QUALITY_ORANGE:
                                isUp = targetHistoryWishBuilder?.IncreaseOrange(weapon) ?? false;
                                itemCounter.OrangeWeapon.IncreaseByOne(weapon);
                                break;
                            case QualityType.QUALITY_PURPLE:
                                targetHistoryWishBuilder?.IncreasePurple(weapon);
                                itemCounter.PurpleWeapon.IncreaseByOne(weapon);
                                break;
                            case QualityType.QUALITY_BLUE:
                                targetHistoryWishBuilder?.IncreaseBlue(weapon);
                                itemCounter.BlueWeapon.IncreaseByOne(weapon);
                                break;
                        }

                        standardWishBuilder.Track(item, weapon, isUp);
                        avatarWishBuilder.Track(item, weapon, isUp);
                        weaponWishBuilder.Track(item, weapon, isUp);
                        chronicledWishBuilder.Track(item, weapon, isUp);
                        noviceWishBuilder.Track(item, weapon, isUp);
                        beyondStandardWishBuilder.Track(item, weapon, isUp);
                        beyondEventWishBuilder.Track(item, weapon, isUp);
                        break;
                    }

                default:
                    // ItemId string length not correct.
                    EllenException.GachaStatisticsInvalidItemId(item.ItemId);
                    break;
            }
        }

        foreach (ref readonly BeyondGachaItem item in context.BeyondItems.AsSpan())
        {
            // Find target history wish to operate. // banner.From <= item.Time <= banner.To
            HistoryWishBuilder? targetHistoryWishBuilder = default;
            if (item.GachaType is not (GachaType.Standard or GachaType.NewBie) && historyWishBuilderMap.TryGetValue(item.GachaType, out List<HistoryWishBuilder>? builders))
            {
                targetHistoryWishBuilder = builders.BinarySearch(item, static (pinned, banner) => pinned.Time < banner.From ? -1 : pinned.Time > banner.To ? 1 : 0);
            }

            switch (item.ItemId.StringLength)
            {
                case 6u:
                    {
                        BeyondItem beyondItem = context.Metadata.GetBeyondItem(item.ItemId);

                        bool isUp = false;
                        switch (beyondItem.Quality)
                        {
                            case QualityType.QUALITY_ORANGE:
                                itemCounter.OrangeBeyondItem.IncreaseByOne(beyondItem);
                                isUp = targetHistoryWishBuilder?.IncreaseOrange(beyondItem) ?? false;
                                break;
                            case QualityType.QUALITY_PURPLE:
                                itemCounter.PurpleBeyondItem.IncreaseByOne(beyondItem);
                                targetHistoryWishBuilder?.IncreasePurple(beyondItem);
                                break;
                        }

                        beyondStandardWishBuilder.Track(item, beyondItem, isUp);
                        beyondEventWishBuilder.Track(item, beyondItem, isUp);
                        break;
                    }

                default:
                    // ItemId string length not correct.
                    EllenException.GachaStatisticsInvalidItemId(item.ItemId);
                    break;
            }
        }

        AsyncBarrier barrier = new(7);

        ImmutableArray<HistoryWish> historyWishes =
        [
            .. context.HistoryWishBuilders
                .Where(b => context.IsEmptyHistoryWishVisible || !b.IsEmpty)
                .OrderByDescending(builder => builder.From)
                .ThenBy(builder => builder.ConfigType, GachaTypeComparer.Shared)
                .Select(builder => builder.ToHistoryWish())
        ];

        TypedWishSummary standardWish = standardWishBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary beyondStandardWish = beyondStandardWishBuilder.ToTypedWishSummary(barrier);
        TypedWishSummary beyondEventWish = beyondEventWishBuilder.ToTypedWishSummary(barrier);
        
        return new()
        {
            // History
            HistoryWishes = historyWishes.AsAdvancedCollectionView(),

            // Avatars
            OrangeAvatars = itemCounter.OrangeAvatar.ToStatisticsImmutableArray(),
            PurpleAvatars = itemCounter.PurpleAvatar.ToStatisticsImmutableArray(),

            // Weapons
            OrangeWeapons = itemCounter.OrangeWeapon.ToStatisticsImmutableArray(),
            PurpleWeapons = itemCounter.PurpleWeapon.ToStatisticsImmutableArray(),
            BlueWeapons = itemCounter.BlueWeapon.ToStatisticsImmutableArray(),

            // Typed wish summary
            StandardWish = standardWish,
            AvatarWish = avatarWishBuilder.ToTypedWishSummary(barrier),
            WeaponWish = weaponWishBuilder.ToTypedWishSummary(barrier),
            ChronicledWish = chronicledWishBuilder.ToTypedWishSummary(barrier),
            NoviceWish = noviceWishBuilder.ToTypedWishSummary(barrier),
            BeyondStandardWish = beyondStandardWish,
            BeyondEventWish = beyondEventWish,
        };
    }
}
