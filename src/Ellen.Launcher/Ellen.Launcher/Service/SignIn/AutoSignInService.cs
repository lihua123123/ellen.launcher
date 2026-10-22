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
using BindingUser = Ellen.Launcher.ViewModel.User.User;
using EntityUser = Ellen.Launcher.Model.Entity.User;

namespace Ellen.Launcher.Service.SignIn;

[Service(ServiceLifetime.Singleton, typeof(IAutoSignInService))]
public sealed partial class AutoSignInService : IAutoSignInService
{
    /// <summary>
    /// 上次自动签到全部成功后，12 小时内不再自动签到。
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(12);

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

        // 打开软件时先看上次自动签到时间：12 小时内已经全部签成功过就不再请求接口
        if (IsInCooldown())
        {
            return;
        }

        List<string> failures = [];
        bool signedAnyRole = false;

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
                // 一个账号下所有已注册的游戏：原神 / 崩坏：星穹铁道 / 绝区零
                ImmutableArray<SignInGameRole> roles = await roleCollectionService.GetRolesAsync(user, token).ConfigureAwait(false);
                if (roles.IsEmpty)
                {
                    failures.Add(SH.FormatServiceSignInAutoSignInFailedItem(
                        user.UserInfo?.Nickname ?? SH.ServiceSignInAutoSignInFailedUnknownUser,
                        SH.ServiceSignInAutoSignInNoGameRole));
                    continue;
                }

                ISignInClient signInClient = signInClientFactory.Create(user.IsOversea);

                foreach (SignInGameRole role in roles)
                {
                    signedAnyRole = true;
                    Response<SignInResult> response = await signInClient.SignAsync(user.Entity, role, token).ConfigureAwait(false);

                    // A successful sign-in returns 0, and "already signed in today" must not be
                    // reported as a failure.
                    if (response.ReturnCode is 0 || (KnownReturnCode)response.ReturnCode is KnownReturnCode.AlreadySignedIn)
                    {
                        continue;
                    }

                    failures.Add(SH.FormatServiceSignInAutoSignInFailedItem(role.DisplayName, response.Message));
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

        if (failures.Count is 0)
        {
            // 只有全部成功才记录时间；有失败的话下次启动会重试
            if (signedAnyRole)
            {
                MarkSucceeded();
            }

            return;
        }

        await ShowFailureDialogAsync(failures).ConfigureAwait(false);
    }


    private static bool IsInCooldown()
    {
        long lastSuccessTime = LocalSetting.Get(SettingKeys.SignInAutoSignInLastSuccessTime, 0L);
        if (lastSuccessTime <= 0)
        {
            return false;
        }

        TimeSpan elapsed = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(lastSuccessTime);
        return elapsed >= TimeSpan.Zero && elapsed < Cooldown;
    }

    private static void MarkSucceeded()
    {
        LocalSetting.Set(SettingKeys.SignInAutoSignInLastSuccessTime, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
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
