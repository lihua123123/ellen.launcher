// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Model.Endgame;
using Ellen.Launcher.Service.Endgame;
using Ellen.Launcher.Service.HardChallenge;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.ViewModel.User;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.ViewModel.HardChallenge;

[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class HardChallengeViewModel : Abstraction.ViewModel, IRecipient<UserAndUidChangedMessage>
{
    private readonly IHardChallengeService hardChallengeService;
    private readonly IHardChallengeGuideService hardChallengeGuideService;
    private readonly IMetadataService metadataService;
    private readonly ITaskContext taskContext;
    private readonly IUserService userService;
    private readonly IMessenger messenger;

    private HardChallengeMetadataContext? metadataContext;

    [GeneratedConstructor]
    public partial HardChallengeViewModel(IServiceProvider serviceProvider);

    public IAdvancedCollectionView<HardChallengeView>? HardChallengeEntries
    {
        get;
        set
        {
            AdvancedCollectionViewCurrentChanged.Detach(field, OnCurrentHardChallengeEntryChanged);
            SetProperty(ref field, value);
            AdvancedCollectionViewCurrentChanged.Attach(field, OnCurrentHardChallengeEntryChanged);
        }
    }
    [ObservableProperty]
    public partial ImmutableArray<AvatarView> BlingAvatars { get; set; }

    /// <summary>
    /// 随应用发布的幽境 Boss 攻略（按版本升序）。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<HardChallengeBossGuidePeriod> BossGuidePeriods { get; set; } = [];

    /// <summary>
    /// 未登录（或该期无记录）时展示的卡片，取自当前期数对应的版本攻略。
    /// </summary>
    [ObservableProperty]
    public partial ImmutableArray<ChallengeView> GuideCards { get; set; } = [];

    /// <summary>
    /// 是否处于「只看攻略」状态（当前期数没有个人挑战记录）。
    /// </summary>
    [ObservableProperty]
    public partial bool IsGuideOnlyMode { get; set; } = true;

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is { } userAndUid)
        {
            UpdateHardChallengeCollectionAsync(userAndUid).SafeForget();
        }
        else
        {
            HardChallengeEntries?.MoveCurrentTo(default);
        }
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        await UpdateBossGuideAsync().ConfigureAwait(false);

        if (!await metadataService.InitializeAsync().ConfigureAwait(false))
        {
            return true;
        }

        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
        {
            metadataContext = await metadataService.GetContextAsync<HardChallengeMetadataContext>(token).ConfigureAwait(false);
            metadataContext.BossGuidePeriods = BossGuidePeriods;
            await UpdateHardChallengeCollectionAsync(userAndUid).ConfigureAwait(false);
        }
        else
        {
            messenger.Send(InfoBarMessage.Warning(SH.MustSelectUserAndUid));
        }

        return true;
    }

    private void OnCurrentHardChallengeEntryChanged(object? sender, object? e)
    {
        HardChallengeEntries?.CurrentItem?.DataEntries?.MoveCurrentToFirst();
        UpdateGuideOnlyMode();
    }

    /// <summary>
    /// 当前选中的这一期没有个人记录（未登录、或该期未打过）时，退回到该期对应版本的内置攻略。
    /// </summary>
    private void UpdateGuideOnlyMode()
    {
        HardChallengeView? current = HardChallengeEntries?.CurrentItem;
        IsGuideOnlyMode = current is not { Engaged: true };

        // 游戏内期数 Id 与「第 N 期」的 N 相差这个基数，Schedule 展示也是这么算的
        int scheduleNumber = current is null ? 0 : (int)(current.ScheduleId - 5269000);
        HardChallengeBossGuidePeriod? period = current is null
            ? HardChallengeBossGuideLookup.FindPeriodByNumber(BossGuidePeriods, 0)
            : HardChallengeBossGuideLookup.FindPeriodByNumber(BossGuidePeriods, scheduleNumber);

        GuideCards = period is null
            ? []
            : [.. period.Bosses.Select(ChallengeView.FromGuide)];
    }

    /// <summary>
    /// 攻略数据完全来自本地资源，因此即使未登录（或元数据不可用）也应能展示。
    /// </summary>
    private async ValueTask UpdateBossGuideAsync()
    {
        ImmutableArray<HardChallengeBossGuidePeriod> periods = await hardChallengeGuideService.GetPeriodsAsync().ConfigureAwait(false);

        await taskContext.SwitchToMainThreadAsync();
        BossGuidePeriods = periods;
        UpdateGuideOnlyMode();
    }

    [SuppressMessage("", "SH003")]
    private async Task UpdateHardChallengeCollectionAsync(UserAndUid userAndUid)
    {
        if (metadataContext is null)
        {
            return;
        }

        try
        {
            using (await EnterCriticalSectionAsync().ConfigureAwait(false))
            {
                ObservableCollection<HardChallengeView> collection = await hardChallengeService
                    .GetHardChallengeViewCollectionAsync(metadataContext, userAndUid)
                    .ConfigureAwait(false);

                ImmutableArray<AvatarView> blingAvatars = await hardChallengeService
                    .GetBlingAvatarsAsync(metadataContext, userAndUid)
                    .ConfigureAwait(false);

                IAdvancedCollectionView<HardChallengeView> hardChallengeEntries = collection.AsAdvancedCollectionView();

                await taskContext.SwitchToMainThreadAsync();
                HardChallengeEntries = hardChallengeEntries;
                HardChallengeEntries.MoveCurrentTo(HardChallengeEntries.Source.FirstOrDefault(s => s.Engaged));

                BlingAvatars = blingAvatars;
                UpdateGuideOnlyMode();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Command("RefreshCommand")]
    private async Task RefreshAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Refresh hard challenge record", "HardChallengeViewModel.Command"));

        if (metadataContext is null)
        {
            return;
        }

        if (HardChallengeEntries is not null)
        {
            if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is { } userAndUid)
            {
                try
                {
                    using (await EnterCriticalSectionAsync().ConfigureAwait(false))
                    {
                        await hardChallengeService
                            .RefreshHardChallengeAsync(metadataContext, userAndUid)
                            .ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }

                await taskContext.SwitchToMainThreadAsync();
                HardChallengeEntries.MoveCurrentTo(HardChallengeEntries.Source.FirstOrDefault(s => s.Engaged));
            }
        }
    }
}