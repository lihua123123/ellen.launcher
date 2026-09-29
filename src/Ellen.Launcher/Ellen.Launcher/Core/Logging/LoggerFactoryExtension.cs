// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.IO.Http.Proxy;
using Ellen.Launcher.Win32;
using System.Runtime.CompilerServices;

namespace Ellen.Launcher.Core.Logging;

public static class LoggerFactoryExtension
{
    extension(ILoggingBuilder builder)
    {
        public ILoggingBuilder AddSentryTelemetry()
        {
            return builder.AddSentry(options =>
            {
                options.HttpProxy = HttpProxyUsingSystemProxy.Instance;

                // Fork default: no telemetry endpoint is configured, so nothing is uploaded.
                // Fill in "Ellen::Telemetry::SentryDsn" in settings.json to opt in.
                options.Dsn = SentryTelemetry.Dsn;

#if DEBUG
                options.Debug = true;
#endif

                options.AutoSessionTracking = true;
                options.IsGlobalModeEnabled = true;
                options.EnableBackpressureHandling = true;
                options.Release = $"{EllenRuntime.Version}";
                options.Environment = GetBuildEnvironment();

                // Suppress logs to generate events and breadcrumbs
                options.MinimumBreadcrumbLevel = LogLevel.None;
                options.MinimumEventLevel = LogLevel.None;

                options.ProfilesSampleRate = 1.0D;
                options.TracesSampleRate = 1.0D;

                // Use our own exception handling
                options.DisableWinUiUnhandledExceptionIntegration();

                options.ConfigureScope(scope =>
                {
                    scope.User = new()
                    {
                        Id = EllenRuntime.DeviceId,
                    };

                    scope.SetTag("elevated", EllenRuntime.IsProcessElevated ? "yes" : "no");
                    scope.SetWebView2Version();
                });

                options.AddExceptionProcessor(new SentryExceptionProcessor());

                options.SetBeforeSend(@event =>
                {
                    Sentry.Protocol.OperatingSystem operatingSystem = @event.Contexts.OperatingSystem;
                    EllenPrivateWindowsVersion windowsVersion = EllenNative.Instance.GetCurrentWindowsVersion();
                    operatingSystem.Build = $"{windowsVersion.Build}";
                    operatingSystem.Name = "Windows";
                    operatingSystem.Version = $"{windowsVersion}";

                    return @event;
                });
            });
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string GetBuildEnvironment()
    {
#if DEBUG
        return "DEBUG";
#elif IS_ALPHA_BUILD
        return "ALPHA";
#elif IS_CANARY_BUILD
        return "CANARY";
#else
        return "RELEASE";
#endif
    }

    extension(Scope scope)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetWebView2Version()
        {
            WebView2Version webView2Version = EllenRuntime.WebView2Version;
            Dictionary<string, object> webView2 = new()
            {
                ["Supported"] = webView2Version.Supported,
                ["Version"] = webView2Version.RawVersion,
            };

            scope.Contexts["WebView2"] = webView2;
        }
    }
}