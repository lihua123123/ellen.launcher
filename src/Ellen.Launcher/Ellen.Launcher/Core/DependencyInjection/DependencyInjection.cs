// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Windows.Globalization;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Domain.WikiSource;
using Ellen.Launcher.Model.Entity.Database;
using Ellen.Launcher.Service;
using Ellen.Launcher.Service.EnemyArchive;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.Web.Response;
using System.Globalization;
using System.Reflection;
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
            .AddSingleton<IMessenger, WeakReferenceMessenger>()

            // P3 ellen.wiki 数据上游（默认 UI 仍是 legacy-722-bundled；切 enemy-web-753 才启用同步）
            // 运行时源默认走真实 HTTP（UA/限速/退避/ETag 增量缓存 + 内容寻址图片缓存）；
            // 设 ELLEN_WIKI_RUNTIME=file 可切回本地镜像（开发/离线测试用），镜像缺失时显式告警。
            .AddSingleton(WikiMirrorPaths.FromEnvironment())
            .AddSingleton<WikiDiagnosticsLog>()
            .AddSingleton<IWikiFlagStore, Service.EnemyArchive.LocalSettingWikiFlagStore>()
            .AddSingleton(sp =>
            {
                WikiSourceFeatureFlag flag = new(sp.GetRequiredService<IWikiFlagStore>());
                WikiCacheStore cache = sp.GetRequiredService<WikiCacheStore>();

                // P5a 灰度：默认 off（沿用持久化设置）；whitelist 只对白名单安装生效；all 才全量。
                // ELLEN_WIKI_UI_SOURCE 仅用于开发/性能演练，优先级最高。
                WikiRolloutDecision rollout = WikiRolloutPolicy.Resolve(
                    flag.UiSource,
                    WikiRolloutPolicy.ParseMode(Environment.GetEnvironmentVariable(WikiRolloutPolicy.RolloutEnvironment)),
                    WikiRolloutPolicy.ResolveInstallId(),
                    WikiRolloutPolicy.ReadWhitelist(cache.Root),
                    Environment.GetEnvironmentVariable(WikiRolloutPolicy.UiSourceOverrideEnvironment));
                flag.ApplyRollout(rollout);
                return flag;
            })
            .AddSingleton(sp => new WikiCacheStore(Environment.GetEnvironmentVariable("ELLEN_WIKI_CACHE")
                ?? System.IO.Path.Combine(EllenRuntime.LocalCacheDirectory, "Wiki")))
            .AddSingleton(sp => HttpWikiSourceOptions.FromEnvironment(sp.GetRequiredService<WikiCacheStore>().Root))
            .AddSingleton<IEnemyWebSource>(sp => CreateEnemySource(sp))
            .AddSingleton<ILauncherSnapshotSource>(sp => CreateSnapshotSource(sp))
            .AddSingleton(sp => new WikiSyncCoordinator(
                sp.GetRequiredService<WikiSourceFeatureFlag>(),
                sp.GetRequiredService<WikiDiagnosticsLog>(),
                _ => sp.GetRequiredService<IEnemyWebSource>().ReadAsync(),
                () => (sp.GetRequiredService<IEnemyWebSource>() as IWikiSourceStatsProvider)?.Stats ?? new WikiCacheStats(),
                () => (sp.GetRequiredService<ILauncherSnapshotSource>() as IWikiSourceStatsProvider)?.Stats ?? new WikiCacheStats()));

        StartupProbe.Mark("di-services");

#if DEBUG
        ServiceProvider serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
#else
        ServiceProvider serviceProvider = services.BuildServiceProvider();
#endif
        StartupProbe.Mark("di-build");
        if (isDefault)
        {
            Ioc.Default.ConfigureServices(serviceProvider);
            StartupProbe.Mark("di-ioc");

            // 后台预热：① 灰度指标（Session 计数 + 崩溃 0 基线，纯文件 I/O）② EF 首次查询。
            // 两者都不依赖 UI 线程，实测合计 ~190ms，放在首帧前会让 M1 白付；并行到 XAML 加载期间。
            _ = Task.Run(() =>
            {
                WarmRolloutMetrics(serviceProvider);
                WarmDatabase(serviceProvider);
            });
            StartupProbe.Mark("di-metrics");
        }

        serviceProvider.InitializeCulture();
        StartupProbe.Mark("di-culture");

        serviceProvider.InitializeNotification();
        StartupProbe.Mark("di-notification");

        return serviceProvider;
    }

    /// <summary>
    /// 后台初始化灰度指标：Session 计数 + rollout-crash.json 0 基线。
    /// 语义不变（进程启动即计一次 Session，不依赖用户是否打开图鉴页），只是不再占用首帧前的 70ms。
    /// 崩溃写入由 <c>Bootstrap.RecordCrash</c> 独立完成，即使这里没跑完也不会丢崩溃记录。
    /// </summary>
    private static void WarmRolloutMetrics(IServiceProvider serviceProvider)
    {
        try
        {
            _ = serviceProvider.GetRequiredService<Service.EnemyArchive.WikiRolloutMetricsService>();
        }
        catch
        {
            // 指标初始化失败不影响启动；缺失会在诊断里显示为无数据（不伪造 0）。
        }
    }

    /// <summary>后台预热 EF：建上下文并把「读设置」这条查询跑一遍，避免首帧前才付模型/连接/查询编译成本。</summary>
    private static void WarmDatabase(IServiceProvider serviceProvider)
    {
        try
        {
            using IServiceScope scope = serviceProvider.CreateScope();
            AppDbContext database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ValidateCompiledModel(database);
            _ = database.Settings.AsNoTracking().FirstOrDefault(e => e.Key == SettingKeys.PrimaryLanguage);
        }
        catch
        {
            // 预热失败不影响启动：真正的读取路径会按自己的错误处理走。
        }
    }

    /// <summary>
    /// 自检：编译模型里每个实体的 ClrType 必须等于对应 <c>DbSet&lt;T&gt;</c> 的泛型参数。
    /// 踩过的坑：编译模型生成到 <c>Ellen.Launcher.Migrations.CompiledModels</c> 时，命名空间嵌套在
    /// <c>Ellen.Launcher.Migrations</c> 之下，生成代码里的 <c>typeof(UidProfilePicture)</c> 绑到了迁移类，
    /// 运行时 <c>DbSet</c> 直接抛「Cannot create DbSet ...」。只记日志不抛——前台崩溃比错绑更难查。
    /// </summary>
    private static void ValidateCompiledModel(AppDbContext database)
    {
        List<string> mismatched = [];
        foreach (PropertyInfo property in typeof(AppDbContext).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.PropertyType.IsGenericType
                || property.PropertyType.GetGenericTypeDefinition() != typeof(DbSet<>))
            {
                continue;
            }

            Type entity = property.PropertyType.GetGenericArguments()[0];
            IEntityType? found = database.Model.FindEntityType(entity);
            if (found is null || found.ClrType != entity)
            {
                mismatched.Add($"{entity.FullName}（模型里是 {found?.ClrType.FullName ?? "缺失"}）");
            }
        }

        if (mismatched.Count > 0)
        {
            Bootstrap.WriteStartupErrorLog(
                new InvalidOperationException($"编译模型与 DbSet 泛型参数不一致：{string.Join("；", mismatched)}"),
                "CompiledModel");
        }
    }

    /// <summary>生产默认真实 HTTP 源；设 ELLEN_WIKI_RUNTIME=file 切本地镜像（开发/离线测试）。</summary>
    private static IEnemyWebSource CreateEnemySource(IServiceProvider sp)
    {
        HttpWikiSourceOptions options = sp.GetRequiredService<HttpWikiSourceOptions>();
        WikiP2Paths paths = sp.GetRequiredService<WikiP2Paths>();
        WikiDiagnosticsLog log = sp.GetRequiredService<WikiDiagnosticsLog>();
        WikiCacheStore cache = sp.GetRequiredService<WikiCacheStore>();

        IEnemyWebSource inner;
        if (string.Equals(Environment.GetEnvironmentVariable("ELLEN_WIKI_RUNTIME"), "file", StringComparison.OrdinalIgnoreCase))
        {
            inner = new FileEnemyWebSource(paths.EnemyDataRoot, options.Endpoint);
        }
        else
        {
            // 敌人源顺带读 C1 manifest（带 ETag，通常 304）以取得 digest/generatedAt 供诊断页展示。
            // 必须复用 DI 里的同一个 C1 实例，否则 C1 统计会被拆成两份。
            HttpLauncherSnapshotSource? snapshot = sp.GetRequiredService<ILauncherSnapshotSource>() as HttpLauncherSnapshotSource;
            inner = new HttpEnemyWebSource(options, cache, log, snapshot);
        }

        /* 753 条数据已经内置进程序集（Resource/EnemyArchives/Web753/bundled-all.json 的 EmbeddedResource），
           随包目录只负责图片与逐条 json 回退 —— 所以这里**无条件**包裹：
           目录缺失/被杀软清掉时依然能读到 753 条，不会再退回内置 722。 */
        return new BundledEnemyWebSource(paths.BundledWebRoot, inner, cache, log);
    }

    private static ILauncherSnapshotSource CreateSnapshotSource(IServiceProvider sp)
    {
        HttpWikiSourceOptions options = sp.GetRequiredService<HttpWikiSourceOptions>();
        WikiP2Paths paths = sp.GetRequiredService<WikiP2Paths>();

        if (string.Equals(Environment.GetEnvironmentVariable("ELLEN_WIKI_RUNTIME"), "file", StringComparison.OrdinalIgnoreCase))
        {
            return new FileLauncherSnapshotSource(paths.SnapshotManifestPath);
        }

        return new HttpLauncherSnapshotSource(
            options,
            sp.GetRequiredService<WikiCacheStore>(),
            sp.GetRequiredService<WikiDiagnosticsLog>());
    }

    extension(IServiceProvider serviceProvider)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCulture()
        {
            CultureOptions cultureOptions = serviceProvider.GetRequiredService<CultureOptions>();
            cultureOptions.SystemCulture = CultureInfo.CurrentCulture;
            StartupProbe.Mark("culture-options");

            // 快速通道：直接读 settings 表拿 PrimaryLanguage。
            // 不能走 cultureOptions.CurrentCulture.Value——那是 EF 首次查询，会把
            // 模型构建 + 迁移枚举 + 查询预热（实测 ~1.2s）全部压在启动主路径上。
            string? savedLanguage = FastSettingReader.TryRead(SettingKeys.PrimaryLanguage);
            CultureInfo cultureInfo = string.IsNullOrEmpty(savedLanguage)
                ? CultureInfo.CurrentCulture
                : CultureInfo.GetCultureInfo(savedLanguage);
            StartupProbe.Mark("culture-read");

            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            StartupProbe.Mark("culture-thread");

            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = cultureInfo.Name;
            }
            catch (COMException)
            {
                // 0x80070032 ERROR_NOT_SUPPORTED
            }
            StartupProbe.Mark("culture-languages");

            SH.Culture = cultureInfo;
            StartupProbe.Mark("culture-sh");
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