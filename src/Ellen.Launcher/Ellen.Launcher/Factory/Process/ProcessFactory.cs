// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.LifeCycle.InterProcess.FullTrust;
using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;
using System.IO;

namespace Ellen.Launcher.Factory.Process;

public sealed class ProcessFactory
{
    public static void KillCurrent()
    {
        global::System.Diagnostics.Process.GetCurrentProcess().Kill();
    }

    public static bool TryGetById(int processId, [NotNullWhen(true)] out IProcess? process)
    {
        try
        {
            process = new DiagnosticsProcess(global::System.Diagnostics.Process.GetProcessById(processId));
            return true;
        }
        catch (Exception ex)
        {
            // Process with an Id of $id$ is not running.
            if (ex is not ArgumentException)
            {
                SentrySdk.CaptureException(ex);
            }
        }

        process = null;
        return false;
    }

    public static bool IsRunning(ReadOnlySpan<string> processNames, [NotNullWhen(true)] out IProcess? runningProcess)
    {
        int currentSessionId = global::System.Diagnostics.Process.GetCurrentProcess().SessionId;
        global::System.Diagnostics.Process[] processes = global::System.Diagnostics.Process.GetProcesses();

        // GetProcesses once and manually loop is O(n)
        foreach (global::System.Diagnostics.Process process in processes)
        {
            try
            {
                if (process.SessionId != currentSessionId)
                {
                    continue;
                }

                if (!process.ProcessName.EqualsAny(processNames, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Force access handle to check whether process has exited
                try
                {
                    _ = process.Handle;
                }
                catch (Exception ex)
                {
                    // Assume it's running if access is denied.
                    if (!EllenNative.IsWin32(ex.HResult, WIN32_ERROR.ERROR_ACCESS_DENIED))
                    {
                        throw;
                    }
                }

                runningProcess = new DiagnosticsProcess(process);
                return true;
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    // 拒绝访问。
                    case Win32Exception we:
                        if (we.NativeErrorCode is (int)WIN32_ERROR.ERROR_ACCESS_DENIED)
                        {
                            runningProcess = new DiagnosticsProcess(process);
                            return true;
                        }

                        break;

                    // Cannot process request because the process ($id$) has exited.
                    case InvalidOperationException ioe:
                        runningProcess = default;
                        return false;
                }

                SentrySdk.CaptureException(ex);
                break;
            }
        }

        runningProcess = default;
        return false;
    }

    public static IProcess CreateUsingShellExecuteRunAs(string arguments, string fileName, string workingDirectory)
    {
        global::System.Diagnostics.Process process = new()
        {
            StartInfo = new()
            {
                Arguments = arguments,
                FileName = fileName,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = workingDirectory,
            },
        };

        return new DiagnosticsProcess(process);
    }

    public static unsafe IProcess CreateSuspended(string arguments, string fileName, string workingDirectory)
    {
        fixed (char* pArguments = arguments)
        {
            fixed (char* pFileName = fileName)
            {
                fixed (char* pWorkingDirectory = workingDirectory)
                {
                    EllenNativeProcessStartInfo startInfo = new()
                    {
                        ApplicationName = pFileName,
                        CommandLine = pArguments,
                        InheritHandles = BOOL.FALSE,
                        CreationFlags = Win32.System.Threading.PROCESS_CREATION_FLAGS.CREATE_SUSPENDED,
                        CurrentDirectory = pWorkingDirectory,
                    };

                    return new NativeProcess(EllenNative.Instance.MakeProcess(startInfo));
                }
            }
        }
    }

    public static IProcess CreateUsingFullTrustSuspended(string arguments, string fileName, string workingDirectory)
    {
        string repoDirectory = EllenRuntime.GetDataRepositoryDirectory();
        string fullTrustFilePath = Path.Combine(repoDirectory, "Snap.ContentDelivery", "Ellen.Launcher.FullTrust.exe");
        StartUsingShellExecuteRunAs(fullTrustFilePath);

        FullTrustProcessStartInfoRequest request = new()
        {
            ApplicationName = fileName,
            CommandLine = arguments,
            CreationFlags = Win32.System.Threading.PROCESS_CREATION_FLAGS.CREATE_SUSPENDED,
            CurrentDirectory = workingDirectory,
        };

        return new FullTrustProcess(request);
    }

    public static void StartUsingShellExecute(string arguments, string fileName)
    {
        global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
        {
            Arguments = arguments,
            FileName = fileName,
            UseShellExecute = true,
        });
    }

    public static void StartUsingShellExecuteRunAs(string fileName)
    {
        global::System.Diagnostics.Process.Start(new global::System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = true,
            Verb = "runas",
        });
    }

    /// <summary>
    /// 以管理员权限重新启动当前进程；发起成功后当前实例会退出。
    /// </summary>
    /// <returns>是否成功发起了提权重启（用户在 UAC 弹窗点「否」时返回 false）。</returns>
    public static bool TryRestartElevated()
    {
        try
        {
            string? currentProcessPath = Environment.ProcessPath ?? global::System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            ArgumentNullException.ThrowIfNull(currentProcessPath);

            StartUsingShellExecuteRunAs(currentProcessPath);
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            // 用户取消 UAC 时会抛 Win32Exception(ERROR_CANCELLED)，这里不打扰用户
            SentrySdk.CaptureException(ex);
            return false;
        }
    }
}