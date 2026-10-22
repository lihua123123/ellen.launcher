// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Response;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.ViewModel.SignIn;

/// <summary>
/// 首页签到卡片中的一行：某个账号在某个游戏下的角色签到状态。
/// </summary>
public sealed partial class SignInEntry : ObservableObject
{
    private readonly ISignInClient signInClient;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;
    private readonly EntityUser user;

    public SignInEntry(ISignInClient signInClient, ITaskContext taskContext, IMessenger messenger, EntityUser user, SignInGameRole role)
    {
        this.signInClient = signInClient;
        this.taskContext = taskContext;
        this.messenger = messenger;
        this.user = user;

        Role = role;
        Attribution = role.DisplayName;
        Uid = role.Uid;
    }

    public SignInGameRole Role { get; }

    /// <summary>
    /// 昵称 | 游戏名
    /// </summary>
    public string Attribution { get; }

    public string Uid { get; }

    [ObservableProperty]
    public partial bool IsSign { get; set; }

    [ObservableProperty]
    public partial int TotalSignDay { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public string TotalSignDayText
    {
        get => SH.FormatViewCardSignInMonthDays(TotalSignDay);
    }

    public async Task UpdateAsync(CancellationToken token = default)
    {
        Response<SignInRewardInfo> response = await signInClient.GetInfoAsync(user, Role, token).ConfigureAwait(false);

        if (!ResponseValidator.TryValidateWithoutUINotification(response, out SignInRewardInfo? info))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        IsSign = info.IsSign;
        TotalSignDay = info.TotalSignDay;
        OnPropertyChanged(nameof(TotalSignDayText));
    }

    /// <summary>
    /// 签到。返回是否已签（签到成功或今日已签都算已签）。
    /// </summary>
    public async Task<bool> SignAsync(CancellationToken token = default)
    {
        if (IsSign)
        {
            return true;
        }

        await taskContext.SwitchToMainThreadAsync();
        IsBusy = true;

        try
        {
            Response<SignInResult> response = await signInClient.SignAsync(user, Role, token).ConfigureAwait(false);

            // 0 为签到成功，-5003 为今日已签到，两者都按成功处理
            if (response.ReturnCode is 0 || (KnownReturnCode)response.ReturnCode is KnownReturnCode.AlreadySignedIn)
            {
                await UpdateAsync(token).ConfigureAwait(false);

                await taskContext.SwitchToMainThreadAsync();
                IsSign = true;
                return true;
            }

            await taskContext.SwitchToMainThreadAsync();
            messenger.Send(InfoBarMessage.Error(SH.ViewCardSignInTitle, response.Message));
            return false;
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsBusy = false;
        }
    }
}
