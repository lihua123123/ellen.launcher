// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.ViewModel.SignIn;

/// <summary>
/// 首页签到卡片中的一行：一个游戏角色（UID）的签到状态。
/// </summary>
public sealed partial class SignInEntry : ObservableObject
{
    private readonly ISignInClient signInClient;
    private readonly ITaskContext taskContext;
    private readonly IMessenger messenger;

    public SignInEntry(ISignInClient signInClient, ITaskContext taskContext, IMessenger messenger, UserAndUid userAndUid, UserGameRole role)
    {
        this.signInClient = signInClient;
        this.taskContext = taskContext;
        this.messenger = messenger;

        UserAndUid = userAndUid;
        Attribution = $"{role.Nickname} | {role.RegionName}";
        Uid = role.GameUid;
    }

    public UserAndUid UserAndUid { get; }

    /// <summary>
    /// 昵称 | 区服
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
        Response<SignInRewardInfo> response = await signInClient.GetInfoAsync(UserAndUid, token).ConfigureAwait(false);

        if (!ResponseValidator.TryValidateWithoutUINotification(response, out SignInRewardInfo? info))
        {
            return;
        }

        await taskContext.SwitchToMainThreadAsync();
        IsSign = info.IsSign;
        TotalSignDay = info.TotalSignDay;
        OnPropertyChanged(nameof(TotalSignDayText));
    }

    [Command("SignInCommand")]
    private async Task SignInAsync()
    {
        if (IsBusy || IsSign)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Response<SignInResult> response = await signInClient.SignAsync(UserAndUid).ConfigureAwait(false);

            // 0 为签到成功，-5003 为今日已签到，两者都按成功处理
            if (response.ReturnCode is 0 || (KnownReturnCode)response.ReturnCode is KnownReturnCode.AlreadySignedIn)
            {
                await UpdateAsync().ConfigureAwait(false);
                return;
            }

            await taskContext.SwitchToMainThreadAsync();
            messenger.Send(InfoBarMessage.Error(SH.ViewCardSignInTitle, response.Message));
        }
        finally
        {
            await taskContext.SwitchToMainThreadAsync();
            IsBusy = false;
        }
    }
}
