// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Ledger;
using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.ViewModel.TravelersDiary;

/// <summary>
/// 首页「旅行者札记」卡片：展示当前账号今日 / 本月的原石与摩拉收入，以及本月收入来源占比。
/// </summary>
[BindableCustomPropertyProvider]
[Service(ServiceLifetime.Transient)]
public sealed partial class TravelersDiaryViewModelSlim : Abstraction.ViewModelSlim, IRecipient<UserAndUidChangedMessage>
{
    private readonly IServiceProvider serviceProvider;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial TravelersDiaryViewModelSlim(IServiceProvider serviceProvider);

    // This property must be a reference type
    [ObservableProperty]
    public partial Ledger? Ledger { get; set; }

    /// <summary>
    /// 切换当前用户后重新拉取札记：卡片是常驻控件，不会随用户切换重建，只能自己刷新。
    /// </summary>
    public void Receive(UserAndUidChangedMessage message)
    {
        LoadAsync().SafeForget();
    }

    /// <inheritdoc/>
    protected override async Task LoadAsync()
    {
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            if (await userService.GetCurrentUserAndUidAsync().ConfigureAwait(false) is not { } userAndUid)
            {
                await taskContext.SwitchToMainThreadAsync();
                Ledger = null;
                IsInitialized = true;
                return;
            }

            try
            {
                IGameRecordClient gameRecordClient = scope.ServiceProvider
                    .GetRequiredService<IOverseaSupportFactory<IGameRecordClient>>()
                    .CreateFor(userAndUid);

                Response<Ledger> response = await gameRecordClient.GetLedgerAsync(userAndUid, 0, CancellationToken.None).ConfigureAwait(false);
                if (ResponseValidator.TryValidateWithoutUINotification(response, out Ledger? ledger))
                {
                    await taskContext.SwitchToMainThreadAsync();
                    Ledger = ledger;
                    IsInitialized = true;
                }
            }
            catch (Exception ex)
            {
                messenger.Send(InfoBarMessage.Error(ex));
            }
        }
    }
}
