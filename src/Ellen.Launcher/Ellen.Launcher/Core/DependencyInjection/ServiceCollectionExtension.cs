// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Ellen.Launcher.Core.Text.Json;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Model.Entity.Database;
using Ellen.Launcher.Win32;
using System.Data.Common;
using System.IO;

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
                Ellen.Launcher.Service.EnemyArchive.StartupProbe.Mark("db-options-enter");

                string dbFile = EllenRuntime.GetDataDirectoryFile("Userdata.db");
                string sqlConnectionString = $"Data Source={dbFile}";

                // 迁移检查的跳过戳：只要「应用版本 + 数据库文件指纹」没变，就不可能有待应用的迁移。
                // 实测这次检查（建上下文 + 枚举 44 个迁移）要 ~490ms，占启动预算的 1/7。
                // 约束：新增迁移必须同时递增 <Version>（发版规则本就如此），否则戳命中会漏掉迁移。
                string stampFile = dbFile + ".schema-stamp";
                string before = SchemaStamp(dbFile);
                if (!string.Equals(ReadStamp(stampFile), before, StringComparison.Ordinal))
                {
                    try
                    {
                        using (AppDbContext context = AppDbContext.Create(serviceProvider, sqlConnectionString))
                        {
                            Ellen.Launcher.Service.EnemyArchive.StartupProbe.Mark("db-context-built");

                            if (context.Database.GetPendingMigrations().Any())
                            {
                                Ellen.Launcher.Service.EnemyArchive.StartupProbe.Mark("db-pending");
                                serviceProvider.GetRequiredService<ILogger<AppDbContext>>().LogInformation("[Database] Performing AppDbContext Migrations");
                                context.Database.Migrate();
                                Ellen.Launcher.Service.EnemyArchive.StartupProbe.Mark("db-migrated");
                            }
                            else
                            {
                                Ellen.Launcher.Service.EnemyArchive.StartupProbe.Mark("db-pending");
                            }
                        }

                        WriteStamp(stampFile, SchemaStamp(dbFile));
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
                }
                else
                {
                    Ellen.Launcher.Service.EnemyArchive.StartupProbe.Mark("db-skipped");
                }

                builder
#if DEBUG
                    .EnableSensitiveDataLogging()
#endif
                    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                    .UseLoggerFactory(serviceProvider.GetRequiredService<ILoggerFactory>())
                    .UseModel(Ellen.Launcher.DbCompiled.AppDbContextModel.Instance)
                    .UseSqlite(sqlConnectionString);
            }

            // 版本 + 文件大小 + 最后写入时间：任一变化都重跑一次检查（迁移后 mtime 会变，正好再校验一次）。
            static string SchemaStamp(string dbFile)
            {
                FileInfo info = new(dbFile);
                return info.Exists
                    ? $"{EllenRuntime.Version}|{info.Length}|{info.LastWriteTimeUtc.Ticks}"
                    : $"{EllenRuntime.Version}|missing";
            }

            static string? ReadStamp(string path)
            {
                try
                {
                    return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
                }
                catch (IOException)
                {
                    return null;
                }
            }

            static void WriteStamp(string path, string value)
            {
                try
                {
                    File.WriteAllText(path, value);
                }
                catch (IOException)
                {
                    // 写不了就下次再查一遍，不影响正确性。
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}