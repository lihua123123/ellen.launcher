// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Microsoft.Windows.Globalization;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Web.Response;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ellen.Launcher.Core.DependencyInjection;

public static class DependencyInjection
{
    public static ServiceProvider Initialize(bool isDefault = false)
    {
        IServiceCollection services = new ServiceCollection()

            // Microsoft extension
            .AddLogging(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFilter(DbLoggerCategory.Database.Command.Name, level => level >= LogLevel.Information)
                    .AddFilter(DbLoggerCategory.Query.Name, level => level >= LogLevel.Information)
                    .AddDebug()
                    .AddSentryTelemetry();
            })
            .AddMemoryCache()

            // Ellen extensions
            .AddJsonOptions()
            .AddDatabase()
            .AddServices()
            .AddResponseValidation()
            .AddConfiguredHttpClients()

            // Discrete services
            .AddSingleton<IMessenger, WeakReferenceMessenger>();

#if DEBUG
        ServiceProvider serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
#else
        ServiceProvider serviceProvider = services.BuildServiceProvider();
#endif

        if (isDefault)
        {
            Ioc.Default.ConfigureServices(serviceProvider);
        }

        serviceProvider.InitializeCulture();
        serviceProvider.InitializeNotification();

        return serviceProvider;
    }

    extension(IServiceProvider serviceProvider)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCulture()
        {
            CultureOptions cultureOptions = serviceProvider.GetRequiredService<CultureOptions>();
            cultureOptions.SystemCulture = CultureInfo.CurrentCulture;

            CultureInfo cultureInfo = cultureOptions.CurrentCulture.Value;

            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;

            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = cultureInfo.Name;
            }
            catch (COMException)
            {
                // 0x80070032 ERROR_NOT_SUPPORTED
            }

            SH.Culture = cultureInfo;
            XamlApplicationLifetime.CultureInfoInitialized = true;

            ILogger<CultureOptions> logger = serviceProvider.GetRequiredService<ILogger<CultureOptions>>();
            logger.LogDebug("System Culture: {System}", cultureOptions.SystemCulture);
            logger.LogDebug("Current Culture: {Current}", cultureInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeNotification()
        {
            _ = serviceProvider.GetRequiredService<IAppNotificationLifeTime>();
        }
    }
}