// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Metadata.ZZZ;
using System.IO;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 绝区零「我的角色」缓存读写。缓存按区服 + UID 分文件，避免多账号互相覆盖。
///
/// 与原神「我的角色」一致：缓存是持久数据，不做过期删除，也不做自动刷新。
/// 只有用户点击「同步角色信息」时才会强制联网更新。
/// </summary>
public interface IZZZAvatarCacheService
{
    ZZZAvatarCache? Load(string uid, string region);

    void Save(ZZZAvatarCache cache);

    ZZZRoleCache? LoadRoles(Guid userInnerId);

    void SaveRoles(ZZZRoleCache cache);
}

[Service(ServiceLifetime.Singleton, typeof(IZZZAvatarCacheService))]
public sealed partial class ZZZAvatarCacheService : IZZZAvatarCacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly ILogger<ZZZAvatarCacheService> logger;

    [GeneratedConstructor]
    public partial ZZZAvatarCacheService(IServiceProvider serviceProvider);

    public ZZZAvatarCache? Load(string uid, string region)
    {
        string path = GetPath(uid, region);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            ZZZAvatarCache? cache = JsonSerializer.Deserialize<ZZZAvatarCache>(stream, SerializerOptions);
            if (cache is null
                || !string.Equals(cache.GameUid, uid, StringComparison.Ordinal)
                || !string.Equals(cache.Region, region, StringComparison.Ordinal)
                || cache.Agents is not { Count: > 0 })
            {
                return null;
            }

            // 旧版本/手工改坏的 JSON 可能把字典写成 null，读取时归一化。
            cache.DetailsJson ??= [];
            cache.WikiAssets ??= new(StringComparer.Ordinal);
            return cache;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read ZZZ avatar cache: {Path}", path);
            return null;
        }
    }

    public void Save(ZZZAvatarCache cache)
    {
        string path = GetPath(cache.GameUid, cache.Region);
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = $"{path}.tmp";
            using (FileStream stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, cache, SerializerOptions);
            }

            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save ZZZ avatar cache: {Path}", path);
        }
    }

    public ZZZRoleCache? LoadRoles(Guid userInnerId)
    {
        string path = GetRolesPath(userInnerId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            ZZZRoleCache? cache = JsonSerializer.Deserialize<ZZZRoleCache>(stream, SerializerOptions);
            if (cache is null || cache.UserInnerId != userInnerId || cache.Roles is not { Count: > 0 })
            {
                return null;
            }

            return cache;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read ZZZ role cache: {Path}", path);
            return null;
        }
    }

    public void SaveRoles(ZZZRoleCache cache)
    {
        string path = GetRolesPath(cache.UserInnerId);
        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = $"{path}.tmp";
            using (FileStream stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, cache, SerializerOptions);
            }

            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save ZZZ role cache: {Path}", path);
        }
    }

    private static string GetPath(string uid, string region)
    {
        string fileName = $"{Sanitize(region)}_{Sanitize(uid)}.json";
        return Path.Combine(EllenRuntime.GetDataServerCacheDirectory(), "ZZZ", "Avatar", fileName);
    }

    private static string GetRolesPath(Guid userInnerId)
    {
        return Path.Combine(EllenRuntime.GetDataServerCacheDirectory(), "ZZZ", "Avatar", $"roles_{userInnerId:N}.json");
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string([.. value.Select(character => invalid.Contains(character) ? '_' : character)]);
    }
}
