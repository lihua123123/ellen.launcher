// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.EnemyArchive;
using System.Collections.Frozen;
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
    private const string IconMapRelativePath = "Resource/EnemyArchives/MonsterIconMap.json";
    private const string UnavailableIconRelativePath = "Resource/EnemyArchives/MonsterIconUnavailable.json";

    private readonly ILogger<EnemyArchiveService> logger;

    private ImmutableArray<EnemyArchiveEntry>? entries;
    private FrozenDictionary<string, string>? iconMap;
    private FrozenSet<string>? unavailableIcons;

    [GeneratedConstructor]
    public partial EnemyArchiveService(IServiceProvider serviceProvider);

    public ValueTask<ImmutableArray<EnemyArchiveEntry>> GetEntriesAsync()
    {
        return entries is { } cached ? ValueTask.FromResult(cached) : LoadAsync();
    }

    public ValueTask<FrozenDictionary<string, string>> GetMonsterIconMapAsync()
    {
        return iconMap is { } cached ? ValueTask.FromResult(cached) : LoadIconMapAsync();
    }

    public ValueTask<FrozenSet<string>> GetUnavailableIconNamesAsync()
    {
        return unavailableIcons is { } cached ? ValueTask.FromResult(cached) : LoadUnavailableIconsAsync();
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

    private async ValueTask<FrozenDictionary<string, string>> LoadIconMapAsync()
    {
        string path = InstalledLocation.GetAbsolutePath(IconMapRelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Monster icon map not found: {Path}", path);
            return FrozenDictionary<string, string>.Empty;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            Dictionary<string, string>? deserialized = await JsonSerializer
                .DeserializeAsync<Dictionary<string, string>>(stream)
                .ConfigureAwait(false);

            FrozenDictionary<string, string> parsed = deserialized is { Count: > 0 }
                ? deserialized.ToFrozenDictionary(StringComparer.Ordinal)
                : FrozenDictionary<string, string>.Empty;

            iconMap = parsed;
            return parsed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load monster icon map from {Path}", path);
            return FrozenDictionary<string, string>.Empty;
        }
    }

    private async ValueTask<FrozenSet<string>> LoadUnavailableIconsAsync()
    {
        string path = InstalledLocation.GetAbsolutePath(UnavailableIconRelativePath);
        if (!File.Exists(path))
        {
            unavailableIcons = FrozenSet<string>.Empty;
            return FrozenSet<string>.Empty;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            string[]? deserialized = await JsonSerializer
                .DeserializeAsync<string[]>(stream)
                .ConfigureAwait(false);

            FrozenSet<string> parsed = deserialized is { Length: > 0 }
                ? deserialized.ToFrozenSet(StringComparer.Ordinal)
                : FrozenSet<string>.Empty;

            unavailableIcons = parsed;
            return parsed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load unavailable monster icons from {Path}", path);
            return FrozenSet<string>.Empty;
        }
    }
}
