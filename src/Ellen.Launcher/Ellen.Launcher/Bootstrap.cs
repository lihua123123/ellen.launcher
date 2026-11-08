// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.LifeCycle.InterProcess;
using Ellen.Launcher.Core.LifeCycle.InterProcess.Model;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Security.Principal;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Service.EnemyArchive;
using Ellen.Launcher.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;
using WinRT;

[assembly: DisableRuntimeMarshalling]

namespace Ellen.Launcher;

[SuppressMessage("", "SH001")]
public static partial class Bootstrap
{
    private const string LockName = "ELLEN_LAUNCHER_BOOTSTRAP_LOCK";

    /// <summary>Toast helper 进程的启动标记；主进程与 helper 共用，改这里即可。</summary>
    public const string ToastHelperArgument = "--toast-helper";
    private static readonly ApplicationInitializationCallback AppInitializationCallback = InitializeApp;
    private static EventWaitHandle? instanceHandle;

    public static void UseNamedPipeRedirection()
    {
        // Clear the field without disposing so the kernel object stays alive
        // for other processes to detect. The using() in Main() handles cleanup.
        instanceHandle = null;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        Ellen.Launcher.Service.EnemyArchive.StartupProbe.Start();

        // Leave a trace of fatal startup failures: without telemetry enabled this is the only
        // way to diagnose why the process disappeared right after launch.
        AppDomain.CurrentDomain.UnhandledException += static (_, e) =>
        {
            WriteStartupErrorLog(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
            RecordCrash("AppDomain.UnhandledException", fatal: true);
        };

        try
        {
            MainCore(args);
        }
        catch (Exception ex)
        {
            WriteStartupErrorLog(ex, "Main");
            RecordCrash("Main", fatal: true);
            throw;
        }
    }

    /// <summary>
    /// P5a W2-1：致命未处理异常计入缓存根 <c>rollout-crash.json</c> 的 <c>CrashCount</c>；
    /// 可恢复 UI 异常用 fatal=false 单独计数，不混算成崩溃。记录失败绝不影响崩溃路径。
    /// </summary>
    public static void RecordCrash(string source, bool fatal)
    {
        try
        {
            string root = Path.Combine(EllenRuntime.LocalCacheDirectory, "Wiki");
            Ellen.Launcher.Domain.WikiSource.WikiRolloutMetricsStore.RecordCrash(root, source, fatal);
        }
        catch
        {
            // Crash recording must never make a bad situation worse.
        }
    }

    public static void WriteStartupErrorLog(Exception? exception, string source)
    {
        try
        {
            string directory = EllenRuntime.DataDirectory;
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                Path.Combine(directory, "startup-error.log"),
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never make a bad situation worse.
        }
    }

    private static void MainCore(string[] args)
    {
        // P5 性能实测（无 UI 数据路径）：必须在单实例/提权/激活之前分流，否则会被重定向到已运行实例。
        string? benchOutput = null;
        foreach (string arg in args)
        {
            if (arg.StartsWith(Ellen.Launcher.Service.EnemyArchive.WikiBenchRunner.ArgumentPrefix, StringComparison.Ordinal))
            {
                benchOutput = arg[Ellen.Launcher.Service.EnemyArchive.WikiBenchRunner.ArgumentPrefix.Length..];
            }
        }

        if (!string.IsNullOrWhiteSpace(benchOutput))
        {
            Environment.ExitCode = Ellen.Launcher.Service.EnemyArchive.WikiBenchRunner.RunAsync(benchOutput).GetAwaiter().GetResult();
            return;
        }

        // Check for toast notification helper process (non-elevated, launched via explorer.exe).
        // The main elevated process creates a named pipe and writes the toast XML before
        // launching this helper. If we can connect to the pipe, we're in helper mode.
        // 只在被主进程以 --toast-helper 拉起时才探测：无条件探测要白等 100ms 管道连接超时（实测 137ms/次）。
        if (Array.IndexOf(args, ToastHelperArgument) >= 0 && TryHandleToastNotificationHelper())
        {
            return;
        }

        StartupProbe.Mark("boot-toast-check");

        // Check if we should restart as administrator
        if (ShouldRestartAsAdmin())
        {
            RestartAsAdministrator();
            return;
        }

        StartupProbe.Mark("boot-admin-check");

        if (!SingleInstanceBypass())
        {
            if (EventWaitHandle.TryOpenExisting(LockName, out _))
            {
                // Another instance is already running. Try a lightweight named pipe
                // redirect and exit quickly; otherwise fall through to full startup
                // (the pipe server may not be ready yet during first instance init).
                if (PrivateNamedPipeClient.TryLightweightRedirect(args))
                {
                    return;
                }
            }
            else
            {
                try
                {
                    instanceHandle = new EventWaitHandle(false, EventResetMode.ManualReset, LockName, out bool created);
                    Debug.Assert(created);
                    EventWaitHandleSecurity handleSecurity = new();
                    handleSecurity.AddAccessRule(new(SecurityIdentifiers.Everyone, EventWaitHandleRights.FullControl, AccessControlType.Allow));
                    instanceHandle.SetAccessControl(handleSecurity);
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    return;
                }
            }
        }

        using (instanceHandle)
        {
            ShowLowWindowsVersionWarningOnce();

            Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00000000");
            Environment.SetEnvironmentVariable("DOTNET_SYSTEM_BUFFERS_SHAREDARRAYPOOL_MAXARRAYSPERPARTITION", "128");
            AppContext.SetData("MVVMTOOLKIT_ENABLE_INOTIFYPROPERTYCHANGING_SUPPORT", false);

            StartupProbe.Mark("boot-single-instance");

            ComWrappersSupport.InitializeComWrappers();
            StartupProbe.Mark("boot-comwrappers");

            // By adding the using statement, we can dispose the injected services when closing
            using (ServiceProvider serviceProvider = DependencyInjection.Initialize(true))
            {
                StartupProbe.Mark("boot-di");

                Thread.CurrentThread.Name = "Ellen Launcher Application Main Thread";

                // Register URL protocol for unpackaged mode
                RuntimeEnvironment.TryRegisterProtocol();
                StartupProbe.Mark("boot-protocol");

                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.DefaultStyleOptimizations);
                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.DeferContextFlyoutInit);
                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.IconNoGridOptimization);
                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.OptimizeApplyStyles);
                StartupProbe.Mark("boot-xaml-options");

                // If you hit a COMException REGDB_E_CLASSNOTREG (0x80040154) during debugging
                // You can delete bin and obj folder and then rebuild.
                // In a Desktop app this runs a message pump publicly,
                // and does not return until the application shuts down.
                StartupProbe.Mark("boot-app-start");
                Application.Start(AppInitializationCallback);
                XamlApplicationLifetime.Exited = true;
            }

            SentrySdk.Flush();
        }
    }

    private static void InitializeApp(ApplicationInitializationCallbackParams param)
    {
        StartupProbe.Mark("app-callback");

        Gen2GcCallback.Register(() =>
        {
            SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateDebug("Gen2 GC triggered.", "Runtime"));
            return true;
        });

        IServiceProvider serviceProvider = Ioc.Default;

        _ = serviceProvider.GetRequiredService<ITaskContext>();
        StartupProbe.Mark("app-taskcontext");

        _ = serviceProvider.GetRequiredService<App>();
        StartupProbe.Mark("app-instance");
    }

    private static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// 开发用：设置环境变量 ELLEN_DISABLE_SINGLE_INSTANCE=1 可跳过单实例互斥，
    /// 允许 Debug 版独立运行（用于截图/多开验证）。不影响正式发布行为。
    /// </summary>
    internal static bool SingleInstanceBypass()
        => string.Equals(Environment.GetEnvironmentVariable("ELLEN_DISABLE_SINGLE_INSTANCE"), "1", StringComparison.Ordinal);

    private static bool ShouldRestartAsAdmin()
    {
        // 开发用旁路：ELLEN_DISABLE_SINGLE_INSTANCE=1 时同时跳过自动提权重启，
        // 避免在无交互（无法弹出 UAC）的环境里启动即退出。
        if (SingleInstanceBypass())
        {
            return false;
        }

        try
        {
            if (IsRunningAsAdministrator())
            {
                return false;
            }

            return LocalSetting.Get(SettingKeys.AutoRestartAsAdmin, false);
        }
        catch
        {
            return false;
        }
    }

    private static void RestartAsAdministrator()
    {
        ProcessFactory.TryRestartElevated();
    }

    private static bool TryHandleToastNotificationHelper()
    {
        using ToastNotificationPipeClient pipeClient = new();
        ToastNotificationRequest? request = pipeClient.TryGetRequest();

        if (request is null)
        {
            return false;
        }

        bool registered = false;

        try
        {
            ComWrappersSupport.InitializeComWrappers();

            string displayName =
#if DEBUG
                "Ellen Launcher Dev";
#else
                "Ellen Launcher";
#endif
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Logo.ico");
            Uri iconUri = new(iconPath);

            AppNotificationManager.Default.Register(displayName, iconUri);
            registered = true;
        }
        catch
        {
            // Toast helper failure is non-fatal
        }

        try
        {
            AppNotification notification = new(request.RawXml)
            {
                SuppressDisplay = request.SuppressDisplay,
            };
            AppNotificationManager.Default.Show(notification);
            Thread.Sleep(500);
        }
        catch
        {
            // Toast helper failure is non-fatal
        }
        finally
        {
            if (registered)
            {
                try
                {
                    AppNotificationManager.Default.Unregister();
                }
                catch
                {
                    // Toast helper failure is non-fatal
                }
            }
        }

        return true;
    }

    private static void ShowLowWindowsVersionWarningOnce()
    {
        if (EllenNative.Instance.IsCurrentWindowsVersionSupported())
        {
            return;
        }

        if (!LocalSetting.Get(SettingKeys.LowWindowsVersionWarningShown, false))
        {
            const string Message = """
                Ellen Launcher 无法在版本低于 10.0.19045.5371 的 Windows 上运行，请更新系统。
                Ellen Launcher cannot run on Windows versions earlier than 10.0.19045.5371. Please update your system.
                """;
            EllenNative.Instance.ShowErrorMessage("Warning | 警告", Message);
            LocalSetting.Set(SettingKeys.LowWindowsVersionWarningShown, true);
        }
    }
}
