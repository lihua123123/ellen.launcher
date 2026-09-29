// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Win32;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Service.Game.Account;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Game.Scheme;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.Web.Hoyolab.Passport;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public sealed class LaunchExecutionGameIdentityHandler : AbstractLaunchExecutionHandler
{
    private byte[]? preLaunchRegistrySnapshot;
    private string? snapshotKeyName;
    private string? snapshotValueName;

    public override async ValueTask BeforeAsync(BeforeLaunchExecutionContext context)
    {
        // Snapshot the current registry value before any launch,
        // so it can be restored if the game clears it during auth ticket login.
        if (context.TargetScheme.SchemeType is not SchemeType.ChineseBilibili)
        {
            (snapshotKeyName, snapshotValueName) = RegistryInterop.GetKeyValueName(context.TargetScheme.SchemeType);
            preLaunchRegistrySnapshot = Registry.GetValue(snapshotKeyName, snapshotValueName, Array.Empty<byte>()) as byte[];
        }

        if (context.LaunchOptions.UsingHoyolabAccount.Value)
        {
            await HandleHoyolabAccountAsync(context).ConfigureAwait(false);
        }
        else if (context.Identity.GameAccount is { } account && !RegistryInterop.Set(account))
        {
            EllenException.Throw(SH.ViewModelLaunchGameSwitchGameAccountFail);
        }
    }

    public override async ValueTask AfterAsync(AfterLaunchExecutionContext context)
    {
        LaunchStatusOptions options = context.ServiceProvider.GetRequiredService<LaunchStatusOptions>();
        await context.TaskContext.SwitchToMainThreadAsync();
        options.UserGameRole = default;

        // When using passport, the login_auth_ticket login may clear or overwrite the
        // registry MIHOYOSDK value set by a previous non-passport launch. Restore the
        // pre-launch snapshot so subsequent non-passport launches can still auto-login.
        if (context.LaunchOptions.UsingHoyolabAccount.Value
            && snapshotKeyName is not null
            && snapshotValueName is not null
            && preLaunchRegistrySnapshot is { Length: > 0 })
        {
            Registry.SetValue(snapshotKeyName, snapshotValueName, preLaunchRegistrySnapshot);
        }
    }

    private static async ValueTask HandleHoyolabAccountAsync(BeforeLaunchExecutionContext context)
    {
        if (context.TargetScheme.SchemeType is SchemeType.ChineseBilibili)
        {
            return;
        }

        if (context.Identity.UserAndUid is not { } userAndUid)
        {
            return;
        }

        if (userAndUid.IsOversea ^ context.TargetScheme.IsOversea)
        {
            EllenException.NotSupported(SH.ViewModelLaunchGameAccountAndServerNotMatch);
        }

        using (IServiceScope scope = context.ServiceProvider.CreateScope())
        {
            IHoyoPlayPassportClient client = scope.ServiceProvider
                .GetRequiredService<IOverseaSupportFactory<IHoyoPlayPassportClient>>()
                .CreateFor(userAndUid);
            Response<AuthTicketWrapper> resp = await client
                .CreateAuthTicketAsync(userAndUid.User)
                .ConfigureAwait(false);

            if (ResponseValidator.TryValidate(resp, scope.ServiceProvider, out AuthTicketWrapper? wrapper))
            {
                IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
                UserGameRole? userGameRole = await userService.GetUserGameRoleByUidAsync(userAndUid.Uid.Value).ConfigureAwait(false);

                await context.TaskContext.SwitchToMainThreadAsync();
                scope.ServiceProvider.GetRequiredService<LaunchStatusOptions>().UserGameRole = userGameRole;

                context.SetOption(LaunchExecutionOptionsKey.LoginAuthTicket, wrapper.Ticket);
                return;
            }
        }

        EllenException.NotSupported(SH.ViewModelLaunchGameCreateAuthTicketFailed);
    }
}