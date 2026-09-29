// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Abstraction;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.ViewModel.GachaLog;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.Service.GachaLog;

[Service(ServiceLifetime.Transient, typeof(IGachaLogWishCountdownService))]
public sealed partial class GachaLogWishCountdownService : IGachaLogWishCountdownService
{
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial GachaLogWishCountdownService(IServiceProvider serviceProvider);

    public async ValueTask<WishCountdownBundle> GetWishCountdownBundleAsync(GachaLogWishCountdownServiceMetadataContext context)
    {
        await taskContext.SwitchToBackgroundAsync();
        return SynchronizedGetWishCountdownBundle(context);
    }

    private static WishCountdownBundle SynchronizedGetWishCountdownBundle(GachaLogWishCountdownServiceMetadataContext context)
    {
        Dictionary<uint, Countdown> idToCountdown = [];

        ImmutableArray<Countdown>.Builder orangeAvatarCountdowns = ImmutableArray.CreateBuilder<Countdown>();
        ImmutableArray<Countdown>.Builder purpleAvatarCountdowns = ImmutableArray.CreateBuilder<Countdown>();
        ImmutableArray<Countdown>.Builder orangeWeaponCountdowns = ImmutableArray.CreateBuilder<Countdown>();
        ImmutableArray<Countdown>.Builder purpleWeaponCountdowns = ImmutableArray.CreateBuilder<Countdown>();

        ImmutableArray<GachaEvent> events = [.. context.GachaEvents.OrderByDescending(b => b.From)];
        foreach (ref readonly GachaEvent gachaEvent in events.AsSpan())
        {
            // Skip events that have not yet started,
            // those events will cause incorrect countdowns
            if (gachaEvent.From > DateTimeOffset.Now)
            {
                continue;
            }

            foreach (uint itemId in gachaEvent.UpOrangeList)
            {
                switch (itemId.StringLength)
                {
                    case 8U:
                        if (!AvatarIds.IsStandardWish(itemId))
                        {
                            TrackAvatarItemId(context, idToCountdown, orangeAvatarCountdowns, gachaEvent, itemId);
                        }

                        break;

                    case 5U:
                        if (!WeaponIds.IsOrangeStandardWish(itemId))
                        {
                            TrackWeaponItemId(context, idToCountdown, orangeWeaponCountdowns, gachaEvent, itemId);
                        }

                        break;

                    default:
                        throw EllenException.NotSupported();
                }
            }

            foreach (uint itemId in gachaEvent.UpPurpleList)
            {
                switch (itemId.StringLength)
                {
                    case 8U:
                        if (!AvatarIds.IsStandardWish(itemId))
                        {
                            TrackAvatarItemId(context, idToCountdown, purpleAvatarCountdowns, gachaEvent, itemId);
                        }

                        break;
                    case 5U:
                        TrackWeaponItemId(context, idToCountdown, purpleWeaponCountdowns, gachaEvent, itemId);
                        break;
                    default:
                        throw EllenException.NotSupported();
                }
            }
        }

        return new()
        {
            OrangeAvatars = orangeAvatarCountdowns.ToImmutable(),
            PurpleAvatars = purpleAvatarCountdowns.ToImmutable(),
            OrangeWeapons = orangeWeaponCountdowns.ToImmutable(),
            PurpleWeapons = purpleWeaponCountdowns.ToImmutable(),
        };
    }

    private static void TrackAvatarItemId(GachaLogWishCountdownServiceMetadataContext context, Dictionary<uint, Countdown> idToCountdown, ImmutableArray<Countdown>.Builder builder, GachaEvent gachaEvent, uint itemId)
    {
        ref Countdown? countdown = ref CollectionsMarshal.GetValueRefOrAddDefault(idToCountdown, itemId, out _);
        if (countdown is null)
        {
            countdown = new(context.GetAvatar(itemId).GetOrCreateItem());
            builder.Insert(0, countdown); // TODO: should add to the end, and reverse the list when completing
        }

        countdown.Histories.Add(new(gachaEvent));
    }

    private static void TrackWeaponItemId(GachaLogWishCountdownServiceMetadataContext context, Dictionary<uint, Countdown> idToCountdown, ImmutableArray<Countdown>.Builder builder, GachaEvent gachaEvent, uint itemId)
    {
        ref Countdown? countdown = ref CollectionsMarshal.GetValueRefOrAddDefault(idToCountdown, itemId, out _);
        if (countdown is null)
        {
            countdown = new(context.GetWeapon(itemId).GetOrCreateItem());
            builder.Insert(0, countdown); // TODO: should add to the end, and reverse the list when completing
        }

        countdown.Histories.Add(new(gachaEvent));
    }
}