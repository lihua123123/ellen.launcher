// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Metadata.ZZZ;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 随包的绝区零调频档期快照（由 <c>tools/ZZZGachaBannerGen/generate.py</c> 生成）。
/// 资源缺失或格式错误时记录日志并返回空表，计时页显示为空但不影响其他功能。
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IZZZGachaBannerService))]
public sealed partial class ZZZGachaBannerService : IZZZGachaBannerService
{
    private const string ResourceRelativePath = "Resource/Metadata/ZZZ/GachaBanner.json";

    private readonly ILogger<ZZZGachaBannerService> logger;
    private readonly Lock gate = new();

    private ImmutableArray<ZZZGachaBanner>? banners;

    [GeneratedConstructor]
    public partial ZZZGachaBannerService(IServiceProvider serviceProvider);

    public ImmutableArray<ZZZGachaBanner> Banners
    {
        get
        {
            if (banners is { } current)
            {
                return current;
            }

            lock (gate)
            {
                if (banners is { } second)
                {
                    return second;
                }

                ImmutableArray<ZZZGachaBanner> loaded = Load();
                banners = loaded;
                return loaded;
            }
        }
    }

    private ImmutableArray<ZZZGachaBanner> Load()
    {
        string path = InstalledLocation.GetAbsolutePath(ResourceRelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("ZZZ gacha banner file not found: {Path}", path);
            return [];
        }

        try
        {
            using (Stream stream = File.OpenRead(path))
            {
                ZZZGachaBannerDataset? dataset = JsonSerializer.Deserialize(stream, ZZZGachaBannerJsonContext.Default.ZZZGachaBannerDataset);
                if (dataset is null)
                {
                    logger.LogWarning("ZZZ gacha banner file is empty: {Path}", path);
                    return [];
                }

                return [.. dataset.Banners];
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load ZZZ gacha banners: {Path}", path);
            return [];
        }
    }
}
