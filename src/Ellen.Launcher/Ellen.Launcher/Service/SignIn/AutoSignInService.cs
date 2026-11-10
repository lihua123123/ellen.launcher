// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Core.LifeCycle;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Response;
using System.Collections.Immutable;
using System.Globalization;
using BindingUser = Ellen.Launcher.ViewModel.User.User;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.Service.SignIn;

[Service(ServiceLifetime.Singleton, typeof(IAutoSignInService))]
public sealed partial class AutoSignInService : IAutoSignInService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ITaskContext taskContext;

    private int hasRun;

    [GeneratedConstructor]
    public partial AutoSignInService(IServiceProvider serviceProvider);

    public async ValueTask SignInAllAsync(CancellationToken token = default)
    {
        // The startup path may run more than once when the network retry coordinator kicks in,
        // but the sign-in itself must only be attempted once per process.
        if (Interlocked.CompareExchange(ref hasRun, 1, 0) is not 0)
        {
            return;
        }

        List<string> failures = [];
        bool changed = false;

        // 今日账本：粒度到「账号 + 角色」。日期不是今天就自动作废，
        // 等价于签到完成后冷却到当天 23:59:59。
        AutoSignInLedger ledger = AutoSignInLedger.LoadOrCreate(DateOnly.FromDateTime(DateTime.Now));

        try
        {
            await taskContext.SwitchToBackgroundAsync();

            using IServiceScope scope = serviceProvider.CreateScope();
            IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            IGameRoleCollectionService roleCollectionService = scope.ServiceProvider.GetRequiredService<IGameRoleCollectionService>();
            IOverseaSupportFactory<ISignInClient> signInClientFactory = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<ISignInClient>>();

            // GetUsersAsync resumes every account, which refreshes the cookie token from the stored
            // SToken and loads all bound game roles.
            AdvancedDbCollectionView<BindingUser, EntityUser> users = await userService.GetUsersAsync().ConfigureAwait(false);
            BindingUser[] snapshot = [.. users.Source];

            foreach (BindingUser user in snapshot)
            {
                // 这个账号今天已经逐个确认过：连角色列表都不用查，零请求。
                // 新加的账号不在账本里，不会被跳过，只会补签它自己缺的部分。
                if (ledger.IsAccountCompleted(user.InnerId))
                {
                    continue;
                }

                // 一个账号下所有已注册的游戏：原神 / 崩坏：星穹铁道 / 绝区零
                ImmutableArray<SignInGameRole> roles = await roleCollectionService.GetRolesAsync(user, token).ConfigureAwait(false);
                if (roles.IsEmpty)
                {
                    failures.Add(SH.FormatServiceSignInAutoSignInFailedItem(
                        user.UserInfo?.Nickname ?? SH.ServiceSignInAutoSignInFailedUnknownUser,
                        SH.ServiceSignInAutoSignInNoGameRole));
                    ledger.MarkAccountIncomplete(user.InnerId);
                    continue;
                }

                ISignInClient signInClient = signInClientFactory.Create(user.IsOversea);
                bool allRolesHandled = true;

                foreach (SignInGameRole role in roles)
                {
                    // 这个角色今天已经确认签过（本次签的、或用户手动签的）：直接跳过。
                    if (ledger.IsRoleHandled(role))
                    {
                        continue;
                    }

                    // 先问一次「今天签了没」：已经签过的只记账，不再白发一遍签到请求。
                    if (await IsSignedTodayAsync(signInClient, user.Entity, role, token).ConfigureAwait(false))
                    {
                        ledger.MarkRoleHandled(role);
                        continue;
                    }

                    Response<SignInResult> response = await signInClient.SignAsync(user.Entity, role, token).ConfigureAwait(false);

                    // A successful sign-in returns 0, and "already signed in today" must not be
                    // reported as a failure.
                    if (response.ReturnCode is 0 || (KnownReturnCode)response.ReturnCode is KnownReturnCode.AlreadySignedIn)
                    {
                        ledger.MarkRoleHandled(role);
                        continue;
                    }

                    allRolesHandled = false;
                    failures.Add(SH.FormatServiceSignInAutoSignInFailedItem(role.DisplayName, response.Message));
                }

                // 整个账号的角色都确认签过才记账：下次启动直接整体跳过；
                // 只要有一个角色失败就不记，下次只补签失败的那几个。
                if (allRolesHandled)
                {
                    ledger.MarkAccountCompleted(user.InnerId);
                }
                else
                {
                    ledger.MarkAccountIncomplete(user.InnerId);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            failures.Add(SH.FormatServiceSignInAutoSignInFailedItem(SH.ServiceSignInAutoSignInFailedUnknownUser, ex.Message));
        }
        finally
        {
            // 中途被取消或抛异常也要把已经确认过的角色写回去，下次启动才不会重复签。
            changed = ledger.Save();
        }

        if (failures.Count is 0)
        {
            if (changed)
            {
                LocalSetting.Set(SettingKeys.SignInAutoSignInLastSuccessTime, DateTimeOffset.Now.ToUnixTimeMilliseconds());
            }

            return;
        }

        await ShowFailureDialogAsync(failures).ConfigureAwait(false);
    }

    /// <summary>
    /// 查询某个角色今天是否已经签到。
    /// 查询失败时返回 <see langword="false"/>，即按「未签」继续走签到流程：
    /// 签到接口对今天已签会返回 -5003，本身是幂等的，多问一次比漏签安全。
    /// </summary>
    private static async ValueTask<bool> IsSignedTodayAsync(ISignInClient signInClient, EntityUser user, SignInGameRole role, CancellationToken token)
    {
        Response<SignInRewardInfo> response = await signInClient.GetInfoAsync(user, role, token).ConfigureAwait(false);
        if (!ResponseValidator.TryValidateWithoutUINotification(response, out SignInRewardInfo? info) || !info.IsSign)
        {
            return false;
        }

        // is_sign 有可能是跨天那几秒里「昨天」的状态：日期对不上就再签一次，让签到接口自己判定。
        if (string.IsNullOrEmpty(info.Today))
        {
            return true;
        }

        return DateTimeOffset.TryParse(info.Today, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset serverToday)
            && serverToday.Date == DateTimeOffset.Now.Date;
    }

    private async ValueTask ShowFailureDialogAsync(List<string> failures)
    {
        await taskContext.SwitchToMainThreadAsync();

        using IServiceScope scope = serviceProvider.CreateScope();
        ICurrentXamlWindowReference currentXamlWindowReference = scope.ServiceProvider.GetRequiredService<ICurrentXamlWindowReference>();

        // The main window is not available yet, there is nothing to attach the dialog to.
        if (currentXamlWindowReference.Window is null)
        {
            return;
        }

        string content = SH.ServiceSignInAutoSignInFailedDialogContent
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, failures);

        IContentDialogFactory contentDialogFactory = scope.ServiceProvider.GetRequiredService<IContentDialogFactory>();
        await contentDialogFactory
            .CreateForConfirmAsync(SH.ServiceSignInAutoSignInFailedDialogTitle, content)
            .ConfigureAwait(false);
    }
}
