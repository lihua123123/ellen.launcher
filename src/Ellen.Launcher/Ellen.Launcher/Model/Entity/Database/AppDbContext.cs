// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Ellen.Launcher.Model.Entity.Configuration;
using System.Diagnostics;

namespace Ellen.Launcher.Model.Entity.Database;

[DebuggerDisplay("Id = {ContextId}")]
public sealed partial class AppDbContext : DbContext
{
    private readonly ILogger<AppDbContext>? logger;

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
        try
        {
            IServiceProviderIsService serviceProviderIsService = this.GetService<IServiceProviderIsService>();
            if (serviceProviderIsService.IsService(typeof(ILogger<AppDbContext>)))
            {
                logger = this.GetService<ILogger<AppDbContext>>();
                logger.LogInformation("AppDbContext::{ContextId} created", ContextId);
            }
        }
        catch
        {
            // ignored
        }
    }

    public DbSet<SettingEntry> Settings { get; set; } = default!;

    public DbSet<User> Users { get; set; } = default!;

    public DbSet<Achievement> Achievements { get; set; } = default!;

    public DbSet<AchievementArchive> AchievementArchives { get; set; } = default!;

    public DbSet<BackpackArchive> BackpackArchives { get; set; } = default!;

    public DbSet<BackpackItem> BackpackItems { get; set; } = default!;

    public DbSet<GachaItem> GachaItems { get; set; } = default!;

    public DbSet<BeyondGachaItem> BeyondGachaItems { get; set; } = default!;

    public DbSet<GachaArchive> GachaArchives { get; set; } = default!;

    public DbSet<AvatarInfo> AvatarInfos { get; set; } = default!;

    public DbSet<GameAccount> GameAccounts { get; set; } = default!;

    public DbSet<DailyNoteEntry> DailyNotes { get; set; } = default!;

    public DbSet<ObjectCacheEntry> ObjectCache { get; set; } = default!;

    public DbSet<CultivateProject> CultivateProjects { get; set; } = default!;

    public DbSet<CultivateEntry> CultivateEntries { get; set; } = default!;

    public DbSet<CultivateEntryLevelInformation> LevelInformation { get; set; } = default!;

    public DbSet<CultivateItem> CultivateItems { get; set; } = default!;

    public DbSet<InventoryItem> InventoryItems { get; set; } = default!;

    public DbSet<SpiralAbyssEntry> SpiralAbysses { get; set; } = default!;

    public DbSet<UidProfilePicture> UidProfilePictures { get; set; } = default!;

    public DbSet<RoleCombatEntry> RoleCombats { get; set; } = default!;

    public DbSet<AvatarStrategy> AvatarStrategies { get; set; } = default!;

    public DbSet<HardChallengeEntry> HardChallenges { get; set; } = default!;

    public static AppDbContext Create(IServiceProvider serviceProvider, string sqlConnectionString)
    {
        // 用编译模型：不传 UseModel 时每次进程首启要反射构建整个模型（实测 1058ms）。
        // 生成命令：dotnet ef dbcontext optimize --context AppDbContext --output-dir Migrations/CompiledModels --namespace Ellen.Launcher.DbCompiled
        // 命名空间必须**不要**嵌在 Ellen.Launcher.Migrations 下：迁移类里也有 UidProfilePicture 等同名类型，
        // 嵌套命名空间会让生成代码里的 typeof(...) 绑到迁移类上，运行时 DbSet<T> 直接抛类型不匹配。
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(sqlConnectionString)
            .UseModel(Ellen.Launcher.DbCompiled.AppDbContextModel.Instance)
            .Options;

        return new(options);
    }

    public override void Dispose()
    {
        base.Dispose();
        logger?.LogInformation("AppDbContext::{ContextId} disposed", ContextId);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfiguration(new AvatarInfoConfiguration())
            .ApplyConfiguration(new DailyNoteEntryConfiguration())
            .ApplyConfiguration(new SpiralAbyssEntryConfiguration())
            .ApplyConfiguration(new RoleCombatEntryConfiguration())
            .ApplyConfiguration(new HardChallengeEntryConfiguration())
            .ApplyConfiguration(new UserConfiguration());
    }
}
