// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.ZZZ;
using Ellen.Launcher.Service.GachaLog;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Web.Endpoint.Upstream;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.GachaLog;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
public sealed partial class WishCountdownViewModel : Abstraction.ViewModelSlim
{
    private readonly IGachaLogWishCountdownService gachaLogWishCountdownService;
    private readonly IMetadataService metadataService;
    private readonly IZZZGachaItemMetadataService zzzGachaItemMetadataService;
    private readonly IZZZGachaBannerService zzzGachaBannerService;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial WishCountdownViewModel(IServiceProvider serviceProvider);

    [ObservableProperty]
    public partial WishCountdownBundle? WishCountdowns { get; set; }

    /// <summary>
    /// 由祈愿记录页在切换游戏 / UID 时驱动。
    /// 原神读取卡池档期事件；绝区零读取随包的官方档期快照。
    /// </summary>
    public async ValueTask UpdateAsync(GachaArchive? archive)
    {
        if (archive is null)
        {
            await taskContext.SwitchToMainThreadAsync();
            WishCountdowns = null;
            return;
        }

        WishCountdownBundle countdowns;
        if (archive.Game is GachaGameType.ZZZ)
        {
            await taskContext.SwitchToBackgroundAsync();
            countdowns = BuildZZZCountdownBundle();
        }
        else
        {
            if (!await metadataService.InitializeAsync().ConfigureAwait(false))
            {
                return;
            }

            GachaLogWishCountdownServiceMetadataContext context = await metadataService.GetContextAsync<GachaLogWishCountdownServiceMetadataContext>().ConfigureAwait(false);
            countdowns = await gachaLogWishCountdownService.GetWishCountdownBundleAsync(context).ConfigureAwait(false);
        }

        await taskContext.SwitchToMainThreadAsync();
        WishCountdowns = countdowns;
        IsInitialized = true;
    }

    /// <summary>
    /// 只保留 S 级代理人 / S 级音擎，按「距离上次 UP 天数」从大到小（上次 UP 时间从早到晚）排列。
    /// 未开始的档期不计入；正在 UP 的档期结束时间在未来，会排在最后。
    /// </summary>
    private WishCountdownBundle BuildZZZCountdownBundle()
    {
        Dictionary<uint, Countdown> orangeAgents = [];
        Dictionary<uint, Countdown> orangeWeapons = [];
        DateTimeOffset now = DateTimeOffset.Now;

        foreach (ZZZGachaBanner banner in zzzGachaBannerService.Banners)
        {
            if (banner.Start > now)
            {
                continue;
            }

            if (!TryGetChannel(banner.Type, out bool isAgent, out string channelName))
            {
                continue;
            }

            string historyLabel = string.IsNullOrEmpty(banner.Version) ? channelName : $"{banner.Version} {channelName}";
            Dictionary<uint, Countdown> target = isAgent ? orangeAgents : orangeWeapons;

            foreach (uint itemId in banner.Up)
            {
                ZZZGachaItemInfo? info = zzzGachaItemMetadataService.GetById(itemId);
                if (info is null || info.IsAgent != isAgent)
                {
                    continue;
                }

                Countdown countdown = GetOrAddCountdown(target, itemId, info);
                AddHistory(countdown, banner.End, historyLabel);
            }
        }

        return new WishCountdownBundle
        {
            OrangeAvatars = ToOrderedImmutableArray(orangeAgents),
            PurpleAvatars = [],
            OrangeWeapons = ToOrderedImmutableArray(orangeWeapons),
            PurpleWeapons = [],
        };
    }

    private static Countdown GetOrAddCountdown(Dictionary<uint, Countdown> target, uint id, ZZZGachaItemInfo info)
    {
        if (target.TryGetValue(id, out Countdown? countdown))
        {
            return countdown;
        }

        countdown = new Countdown(new Item
        {
            Id = id,
            Name = info.Name,
            Icon = info.IconUri ?? StaticResourcesEndpoints.UIIconNone,
            Quality = QualityType.QUALITY_ORANGE,
        });

        target[id] = countdown;
        return countdown;
    }

    private static void AddHistory(Countdown countdown, DateTimeOffset endTime, string historyLabel)
    {
        if (countdown.Histories.Any(history => history.LastTime == endTime && history.FormattedVersionOrder == historyLabel))
        {
            return;
        }

        countdown.Histories.Add(new CountdownHistory(endTime, historyLabel, countdown.Item.Icon));
    }

    private static ImmutableArray<Countdown> ToOrderedImmutableArray(Dictionary<uint, Countdown> source)
    {
        foreach (Countdown countdown in source.Values)
        {
            countdown.Histories.Sort(static (left, right) => right.LastTime.CompareTo(left.LastTime));
        }

        return
        [
            .. source.Values
                .OrderBy(static countdown => countdown.LastTime)
                .ThenBy(static countdown => countdown.Item.Name, StringComparer.Ordinal),
        ];
    }

    private static bool TryGetChannel(int gachaType, out bool isAgent, out string channelName)
    {
        switch (gachaType)
        {
            case 2:
                isAgent = true;
                channelName = SH.WebGachaConfigTypeZZZExclusiveChannel;
                return true;
            case 102:
                isAgent = true;
                channelName = SH.WebGachaConfigTypeZZZExclusiveRescreening;
                return true;
            case 3:
                isAgent = false;
                channelName = SH.WebGachaConfigTypeZZZWEngineChannel;
                return true;
            case 103:
                isAgent = false;
                channelName = SH.WebGachaConfigTypeZZZWEngineReverberation;
                return true;
            default:
                isAgent = false;
                channelName = string.Empty;
                return false;
        }
    }
}