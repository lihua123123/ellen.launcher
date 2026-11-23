// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.LifeCycle.InterProcess;

public static class PrivateNamedPipe
{
    public const int PrivateVersion = 1;
    public const int FullTrustVersion = 1;

    // PrivateName / ToastPipeName 以安装目录为作用域：开发构建与正式安装不再共享
    // 单实例通道。FullTrustName 保持全局常量 —— 主进程与 FullTrust 子进程分处
    // 不同目录（安装目录 vs 数据仓库目录），必须同名才能互连。
    public static string PrivateName { get; } = $"Ellen.Launcher.PrivateNamedPipe.{InstanceScope.Suffix}";

    public const string FullTrustName = "Ellen.Launcher.PrivateFullTrustNamedPipe";

    public static string ToastPipeName { get; } = $"Ellen.Launcher.ToastPipe.{InstanceScope.Suffix}";
}
