// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.LifeCycle.InterProcess;

public static class PrivateNamedPipe
{
    public const int PrivateVersion = 1;
    public const int FullTrustVersion = 1;
    public const string PrivateName = "Ellen.Launcher.PrivateNamedPipe";
    public const string FullTrustName = "Ellen.Launcher.PrivateFullTrustNamedPipe";
    public const string ToastPipeName = "Ellen.Launcher.ToastPipe";
}