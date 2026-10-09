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
        // Leave a trace of fatal startup failures: without telemetry enabled this is the only
        // way to diagnose why the process disappeared right after launch.
        AppDomain.CurrentDomain.UnhandledException += static (_, e) => WriteStartupErrorLog(e.ExceptionObject as Exception, "AppDomain.UnhandledException");

        try
        {
            MainCore(args);
        }
        catch (Exception ex)
        {
            WriteStartupErrorLog(ex, "Main");
            throw;
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
        // Check for toast notification helper process (non-elevated, launched via explorer.exe).
        // The main elevated process creates a named pipe and writes the toast XML before
        // launching this helper. If we can connect to the pipe, we're in helper mode.
        if (TryHandleToastNotificationHelper())
        {
            return;
        }

        // Check if we should restart as administrator
        if (ShouldRestartAsAdmin())
        {
            RestartAsAdministrator();
            return;
        }

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

        using (instanceHandle)
        {
            ShowLowWindowsVersionWarningOnce();

            Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "00000000");
            Environment.SetEnvironmentVariable("DOTNET_SYSTEM_BUFFERS_SHAREDARRAYPOOL_MAXARRAYSPERPARTITION", "128");
            AppContext.SetData("MVVMTOOLKIT_ENABLE_INOTIFYPROPERTYCHANGING_SUPPORT", false);

            ComWrappersSupport.InitializeComWrappers();

            // By adding the using statement, we can dispose the injected services when closing
            using (ServiceProvider serviceProvider = DependencyInjection.Initialize(true))
            {
                Thread.CurrentThread.Name = "Ellen Launcher Application Main Thread";

                // Register URL protocol for unpackaged mode
                RuntimeEnvironment.TryRegisterProtocol();

                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.DefaultStyleOptimizations);
                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.DeferContextFlyoutInit);
                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.IconNoGridOptimization);
                Microsoft.UI.Xaml.Settings.XamlOptionalChanges.EnableChange(Microsoft.UI.Xaml.Settings.XamlChangeId.OptimizeApplyStyles);

                // If you hit a COMException REGDB_E_CLASSNOTREG (0x80040154) during debugging
                // You can delete bin and obj folder and then rebuild.
                // In a Desktop app this runs a message pump publicly,
                // and does not return until the application shuts down.
                Application.Start(AppInitializationCallback);
                XamlApplicationLifetime.Exited = true;
            }

            SentrySdk.Flush();
        }
    }

    private static void InitializeApp(ApplicationInitializationCallbackParams param)
    {
        Gen2GcCallback.Register(() =>
        {
            SentrySdk.AddBreadcrumb(BreadcrumbFactory.CreateDebug("Gen2 GC triggered.", "Runtime"));
            return true;
        });

        IServiceProvider serviceProvider = Ioc.Default;

        _ = serviceProvider.GetRequiredService<ITaskContext>();
        _ = serviceProvider.GetRequiredService<App>();
    }

    private static bool IsRunningAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool ShouldRestartAsAdmin()
    {
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
