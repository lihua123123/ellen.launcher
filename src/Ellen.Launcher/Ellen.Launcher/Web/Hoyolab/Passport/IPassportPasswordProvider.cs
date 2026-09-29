// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Passport;

public interface IPassportPasswordProvider : IAigisProvider, IVerifyProvider
{
    string? Account { get; }

    string? Password { get; }
}