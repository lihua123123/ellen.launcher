// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat;
using Ellen.Launcher.Web.Response;

// 本文件的命名空间尾部同为 RoleCombat，会遮蔽同名类型，因此用别名区分
using RoleCombatRecord = Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat.RoleCombat;

namespace Ellen.Launcher.ViewModel.RoleCombat;

/// <summary>
/// 「月谕圣牌」页。
/// 数据取自幻想真境剧诗接口（<c>role_combat</c>）返回的 <c>tarot_card_state</c>，
/// 只需要账号 cookie 即可获取，不需要启动游戏或注入。
/// </summary>
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Scoped)]
public sealed partial class TarotCardViewModel : Abstraction.ViewModel, IRecipient<UserAndUidChangedMessage>
{
    private readonly IServiceProvider serviceProvider;
    private readonly IUserService userService;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    [GeneratedConstructor]
    public partial TarotCardViewModel(IServiceProvider serviceProvider);

    [ObservableProperty]
    public partial List<TarotCardView>? Cards { get; set; }

    [ObservableProperty]
    public partial int UnlockedCount { get; set; }

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    public string ProgressText { get => SH.FormatViewTarotCardProgress(UnlockedCount, TotalCount); }

    public void Receive(UserAndUidChangedMessage message)
    {
        if (message.UserAndUid is not null)
        {
            UpdateAsync().SafeForget();
        }
    }

    protected override async ValueTask<bool> LoadOverrideAsync(CancellationToken token)
    {
        await UpdateAsync(token).ConfigureAwait(false);
        return true;
    }

    [Command("RefreshCommand")]
    private async Task RefreshAsync()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateUI("Refresh tarot cards", "TarotCardViewModel.Command"));
        await UpdateAsync().ConfigureAwait(false);
    }

    [SuppressMessage("", "SH003")]
    private async Task UpdateAsync(CancellationToken token = default)
    {
        if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
        {
            messenger.Send(InfoBarMessage.Warning(SH.MustSelectUserAndUid));
            return;
        }

        try
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            IGameRecordClient gameRecordClient = scope.ServiceProvider
                .GetRequiredService<IOverseaSupportFactory<IGameRecordClient>>()
                .Create(userAndUid.IsOversea);

            Response<RoleCombatRecord> response = await gameRecordClient
                .GetRoleCombatAsync(userAndUid, token)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidateWithoutUINotification(response, out RoleCombatRecord? roleCombat))
            {
                return;
            }

            TarotCardState? state = roleCombat.TarotCardState;
            List<TarotCardView> cards = state is null
                ? []
                : [.. state.List.Select(static card => TarotCardView.Create(card))];

            await taskContext.SwitchToMainThreadAsync();

            Cards = cards;
            UnlockedCount = state?.CurrentNumber ?? 0;
            TotalCount = state?.TotalNumber ?? 0;
            OnPropertyChanged(nameof(ProgressText));
        }
        catch (OperationCanceledException)
        {
        }
    }
}
