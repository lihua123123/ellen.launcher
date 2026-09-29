// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Ellen.Launcher.Core.Text.Json;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Model.Entity.Database;
using Ellen.Launcher.Win32;
using System.Data.Common;

namespace Ellen.Launcher.Core.DependencyInjection;

public static partial class ServiceCollectionExtension
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static partial IServiceCollection AddServices(this IServiceCollection services);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddJsonOptions()
        {
            return services.AddSingleton(JsonOptions.Default);
        }

        public IServiceCollection AddDatabase()
        {
            return services.AddDbContextPool<AppDbContext>(AddDbContext);

            static void AddDbContext(IServiceProvider serviceProvider, DbContextOptionsBuilder builder)
            {
                string dbFile = EllenRuntime.GetDataDirectoryFile("Userdata.db");
                string sqlConnectionString = $"Data Source={dbFile}";

                try
                {
                    using (AppDbContext context = AppDbContext.Create(serviceProvider, sqlConnectionString))
                    {
                        if (context.Database.GetPendingMigrations().Any())
                        {
                            serviceProvider.GetRequiredService<ILogger<AppDbContext>>().LogInformation("[Database] Performing AppDbContext Migrations");
                            context.Database.Migrate();
                        }
                    }
                }
                catch (DbException ex)
                {
                    string message = $"""
                        Ellen Launcher 在执行数据库迁移时发生错误。
                        Ellen Launcher encountered an error while performing database migration.

                        Database at '{dbFile}'

                        {ex.Message}
                        """;
                    EllenNative.Instance.ShowErrorMessage("Warning | 警告", message);
                    ProcessFactory.KillCurrent();
                    return;
                }

                builder
#if DEBUG
                    .EnableSensitiveDataLogging()
#endif
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                    .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                    .UseSqlite(sqlConnectionString);
            }
        }
    }
}