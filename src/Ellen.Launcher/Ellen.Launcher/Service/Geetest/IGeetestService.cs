// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Hoyolab.Passport;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Upstream.Geetest;

namespace Ellen.Launcher.Service.Geetest;

public interface IGeetestService
{
    ValueTask<GeetestData?> TryVerifyGtChallengeAsync(string gt, string challenge, bool isOversea, CancellationToken token = default);

    ValueTask<string?> TryVerifyXrpcChallengeAsync(Model.Entity.User user, CardVerificationHeaders headers, CancellationToken token = default);

    ValueTask<bool> TryVerifyAigisSessionAsync(IAigisProvider provider, string? rawSession, bool isOversea, CancellationToken token = default);
}