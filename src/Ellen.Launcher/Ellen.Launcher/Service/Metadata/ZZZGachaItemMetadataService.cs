// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Metadata.ZZZ;
using System.Collections.Frozen;
using System.IO;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 随包的绝区零调频物品元数据。资源缺失或格式错误时只记录日志并返回空表，
/// 不影响调频记录页的既有功能（图标会退化为占位图）。
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IZZZGachaItemMetadataService))]
public sealed partial class ZZZGachaItemMetadataService : IZZZGachaItemMetadataService
{
    private const string ResourceRelativePath = "Resource/Metadata/ZZZ/GachaItem.json";

    private readonly ILogger<ZZZGachaItemMetadataService> logger;

    private readonly Lock gate = new();

    private FrozenDictionary<uint, ZZZGachaItemInfo>? items;

    [GeneratedConstructor]
    public partial ZZZGachaItemMetadataService(IServiceProvider serviceProvider);

    public ZZZGachaItemInfo? GetById(uint itemId)
    {
        return Items.TryGetValue(itemId, out ZZZGachaItemInfo? item) ? item : null;
    }

    private FrozenDictionary<uint, ZZZGachaItemInfo> Items
    {
        get
        {
            FrozenDictionary<uint, ZZZGachaItemInfo>? current = Volatile.Read(ref items);
            if (current is not null)
            {
                return current;
            }

            lock (gate)
            {
                current = items;
                if (current is not null)
                {
                    return current;
                }

                current = LoadItems();
                Volatile.Write(ref items, current);
                return current;
            }
        }
    }

    private FrozenDictionary<uint, ZZZGachaItemInfo> LoadItems()
    {
        string path = InstalledLocation.GetAbsolutePath(ResourceRelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("ZZZ gacha item metadata file not found: {Path}", path);
            return FrozenDictionary<uint, ZZZGachaItemInfo>.Empty;
        }

        try
        {
            using (Stream stream = File.OpenRead(path))
            {
                ZZZGachaItemInfoDataset? dataset = JsonSerializer.Deserialize(stream, ZZZGachaItemInfoJsonContext.Default.ZZZGachaItemInfoDataset);
                if (dataset is null)
                {
                    logger.LogWarning("ZZZ gacha item metadata file is empty: {Path}", path);
                    return FrozenDictionary<uint, ZZZGachaItemInfo>.Empty;
                }

                return dataset.Items.ToFrozenDictionary(static item => item.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load ZZZ gacha item metadata: {Path}", path);
            return FrozenDictionary<uint, ZZZGachaItemInfo>.Empty;
        }
    }
}
