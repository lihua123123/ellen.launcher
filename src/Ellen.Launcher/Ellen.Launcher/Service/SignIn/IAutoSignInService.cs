// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.SignIn;

public interface IAutoSignInService
{
    /// <summary>
    /// Signs in every game role of every logged-in account.
    /// The operation is silent when it succeeds; failures are reported to the user with a dialog.
    /// It runs at most once per process lifetime.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    ValueTask SignInAllAsync(CancellationToken token = default);
}
