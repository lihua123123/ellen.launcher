// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Core.LifeCycle;
using Ellen.Launcher.Factory.ContentDialog;
using Ellen.Launcher.Service.User;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Response;
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

        try
        {
            await taskContext.SwitchToBackgroundAsync();

            using IServiceScope scope = serviceProvider.CreateScope();
            IUserService userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            IOverseaSupportFactory<ISignInClient> signInClientFactory = scope.ServiceProvider.GetRequiredService<IOverseaSupportFactory<ISignInClient>>();

            // GetUsersAsync resumes every account, which refreshes the cookie token from the stored
            // SToken and loads all bound game roles, so we can sign in for each role below.
            AdvancedDbCollectionView<BindingUser, EntityUser> users = await userService.GetUsersAsync().ConfigureAwait(false);
            BindingUser[] snapshot = [.. users.Source];

            foreach (BindingUser user in snapshot)
            {
                if (user.UserGameRoles.Count is 0)
                {
                    failures.Add(SH.FormatServiceSignInAutoSignInFailedItem(
                        user.UserInfo?.Nickname ?? SH.ServiceSignInAutoSignInFailedUnknownUser,
                        SH.ServiceSignInAutoSignInNoGameRole));
                    continue;
                }

                ISignInClient signInClient = signInClientFactory.Create(user.IsOversea);

                foreach (UserGameRole role in user.UserGameRoles)
                {
                    UserAndUid userAndUid = UserAndUid.From(user.Entity, role);
                    Response<SignInResult> response = await signInClient.SignAsync(userAndUid, token).ConfigureAwait(false);

                    // A successful sign-in returns 0, and "already signed in today" must not be
                    // reported as a failure.
                    if (response.ReturnCode is 0 || (KnownReturnCode)response.ReturnCode is KnownReturnCode.AlreadySignedIn)
                    {
                        continue;
                    }

                    failures.Add(SH.FormatServiceSignInAutoSignInFailedItem(role.ToString(), response.Message));
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
            return;
        }

        await ShowFailureDialogAsync(failures).ConfigureAwait(false);
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
