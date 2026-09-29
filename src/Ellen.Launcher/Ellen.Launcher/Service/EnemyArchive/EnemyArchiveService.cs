// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.EnemyArchive;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.EnemyArchive;

/// <summary>
/// 从应用目录的 JSON 资源懒加载敌人档案。
/// <para>
/// 与元数据不同，这份资料是社区整理的图鉴文本，随应用发布，因此完全离线可用。
/// 资源缺失或格式错误时只记录日志并返回空集合，不影响敌人图鉴的既有功能。
/// </para>
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IEnemyArchiveService))]
public sealed partial class EnemyArchiveService : IEnemyArchiveService
{
    private const string ResourceRelativePath = "Resource/EnemyArchives/EnemyArchives.json";

    private readonly ILogger<EnemyArchiveService> logger;

    private ImmutableArray<EnemyArchiveEntry>? entries;

    [GeneratedConstructor]
    public partial EnemyArchiveService(IServiceProvider serviceProvider);

    public ValueTask<ImmutableArray<EnemyArchiveEntry>> GetEntriesAsync()
    {
        return entries is { } cached ? ValueTask.FromResult(cached) : LoadAsync();
    }

    private async ValueTask<ImmutableArray<EnemyArchiveEntry>> LoadAsync()
    {
        string path = InstalledLocation.GetAbsolutePath(ResourceRelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Enemy archive file not found: {Path}", path);
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            EnemyArchiveEntry[]? deserialized = await JsonSerializer
                .DeserializeAsync<EnemyArchiveEntry[]>(stream)
                .ConfigureAwait(false);

            ImmutableArray<EnemyArchiveEntry> parsed = deserialized is { Length: > 0 }
                ? [.. deserialized]
                : [];

            entries = parsed;
            return parsed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load enemy archive from {Path}", path);
            return [];
        }
    }
}
