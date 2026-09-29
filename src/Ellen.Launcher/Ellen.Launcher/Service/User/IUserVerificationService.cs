// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Passport;

namespace Ellen.Launcher.Service.User;

public interface IUserVerificationService
{
    ValueTask<bool> TryVerifyAsync(IVerifyProvider provider, string? rawRisk, bool isOversea, CancellationToken token = default);
}