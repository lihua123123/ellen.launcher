// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Ellen.Launcher.Core;

/// <summary>
/// 单实例作用域：以 exe 所在目录为界，推导跨进程同步对象
/// （Bootstrap 引导锁、私有激活管道、Toast 管道）的名字后缀。
/// 开发目录与正式安装目录互不干扰 —— 修掉「点正式版快捷方式却被重定向到
/// 正在运行的开发版」的问题；同一安装目录升级前后仍互认（同目录 = 同作用域）。
/// </summary>
public static class InstanceScope
{
    public static string Suffix { get; } = ComputeSuffix();

    private static string ComputeSuffix()
    {
        string directory = AppContext.BaseDirectory
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .ToUpperInvariant();
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(directory));
        return Convert.ToHexString(hash)[..16];
    }
}
