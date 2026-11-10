// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.IO;
using System.Text.Json;

namespace Ellen.Launcher.Core;

/// <summary>
/// 便携布局：安装器在 exe 同目录写一份 <c>portable.json</c>，声明
/// 「数据/缓存目录跟着安装目录走」以及安装时选的随包图片档位。
///
/// 为什么必须放在设置系统之前读：<c>LocalSetting</c> 的 settings.json 位置在静态构造里
/// 就固定了，之后不可能再靠 settings.json 里的某个键把自己搬走 —— 只能靠 exe 旁边的引导文件。
/// 文件不存在时一切保持原来的 <c>%LOCALAPPDATA%\EllenLauncher</c> 行为，零影响。
/// </summary>
public static class PortableLayout
{
    public const string MarkerFileName = "portable.json";

    public const string ImageQualityCompact = "compact";
    public const string ImageQualityOriginal = "original";

    private static readonly Lazy<PortableConfig?> Config = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    public static bool IsEnabled => Config.Value is not null;

    public static string? DataDirectory => Config.Value?.DataDirectory;

    public static string? ImageQuality => Config.Value?.ImageQuality;

    private static PortableConfig? Load()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, MarkerFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("dataDir", out JsonElement dirElement))
            {
                return null;
            }

            string? dataDir = dirElement.GetString();
            if (string.IsNullOrWhiteSpace(dataDir))
            {
                return null;
            }

            string quality = root.TryGetProperty("imageQuality", out JsonElement qualityElement)
                && string.Equals(qualityElement.GetString(), ImageQualityOriginal, StringComparison.OrdinalIgnoreCase)
                ? ImageQualityOriginal
                : ImageQualityCompact;

            return new PortableConfig(Path.GetFullPath(dataDir), quality);
        }
        catch (Exception)
        {
            // 引导文件坏了不能拖垮启动：退回默认布局
            return null;
        }
    }

    private sealed record PortableConfig(string DataDirectory, string ImageQuality);
}
