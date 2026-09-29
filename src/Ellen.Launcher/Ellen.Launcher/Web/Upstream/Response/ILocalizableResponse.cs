// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Upstream.Response;

public interface ILocalizableResponse
{
    public string? LocalizationKey { get; set; }
}