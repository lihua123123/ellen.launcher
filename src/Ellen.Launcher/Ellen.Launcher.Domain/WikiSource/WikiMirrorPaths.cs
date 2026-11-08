// P2 本地镜像路径配置。默认指向开发仓库的镜像目录；运行时可用环境变量覆盖。
// 未配置/不存在时 WikiEnemyCatalogService 显式告警，页面保持 legacy，绝不静默回退。

using System;
using System.IO;

namespace Ellen.Launcher.Domain.WikiSource;

public static class WikiMirrorPaths
{
    public const string MirrorVariable = "ELLEN_WIKI_MIRROR";

    /// <summary>镜像根目录，其下应有 enemies/、launcher/、reports/ 三个子目录。</summary>
    public static WikiP2Paths FromEnvironment()
    {
        string root = Environment.GetEnvironmentVariable(MirrorVariable) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(root))
        {
            // 开发环境默认：与两个仓库同级的 workspace/wiki-migration 报告 + ellen-wiki 源。
            // 打包安装后该目录不存在 → IsComplete=false → 显式告警，UI 保持 legacy。
            root = "D:/EllenDB";
        }

        string reports = Path.Combine(root, "workspace", "wiki-migration", "reports");
        return new WikiP2Paths
        {
            EnemyDataRoot = Environment.GetEnvironmentVariable("ELLEN_WIKI_ENEMIES")
                ?? Path.Combine(root, "ellen-wiki", "src", "data", "enemies"),
            SnapshotManifestPath = Environment.GetEnvironmentVariable("ELLEN_WIKI_MANIFEST")
                ?? Path.Combine(root, "ellen-wiki", "dist", "api", "launcher", "manifest.json"),
            AliasMapPath = Path.Combine(reports, "alias-statekey-map.json"),
            OrphanListPath = Path.Combine(reports, "orphan-images.json"),
            ImageCacheRoot = Environment.GetEnvironmentVariable("ELLEN_WIKI_IMAGE_CACHE") ?? string.Empty,
            // P5：753 条目 JSON 随包目录。顺序：环境变量 → **安装目录旁的 Resource**（真实安装版）
            // → 开发仓库的发布暂存目录。
            // 为什么要有第二档：安装到用户机器上时 D:/EllenDB 不存在，若只回落到开发路径，
            // HasBundled753 恒为 false，随包 753 在安装版里**永远不生效**（离线首屏直接退化成同步中空态）。
            BundledWebRoot = ResolveBundledRoot(root),
            // P6：随包图片（清单 + 原图 + 256px 缩略图），默认是随包 753 目录下的 assets/。
            // 单独一个环境变量，便于灰度期只换图片包而不动数据包。
            BundledAssetsRoot = ResolveBundledAssetsRoot(root),
        };
    }

    private static string ResolveBundledRoot(string root)
    {
        string? overridden = Environment.GetEnvironmentVariable("ELLEN_WIKI_BUNDLE");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        string installed = Path.Combine(AppContext.BaseDirectory, "Resource", "EnemyArchives", "Web753");
        if (File.Exists(Path.Combine(installed, "index.json")))
        {
            return installed;
        }

        return Path.Combine(root, "ellen-launcher", "Installer", "Publish", "Resource", "EnemyArchives", "Web753");
    }

    private static string ResolveBundledAssetsRoot(string root)
    {
        string? overridden = Environment.GetEnvironmentVariable("ELLEN_WIKI_ASSETS");
        if (!string.IsNullOrWhiteSpace(overridden))
        {
            return overridden;
        }

        string installed = Path.Combine(AppContext.BaseDirectory, "Resource", "EnemyArchives", "Web753", "assets");
        if (Directory.Exists(installed))
        {
            return installed;
        }

        return Path.Combine(root, "ellen-launcher", "Installer", "Publish", "Resource", "EnemyArchives", "Web753", "assets");
    }
}
