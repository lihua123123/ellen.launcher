// P6 随包图片（方案 B 的最后一层）：Web753/assets/ 下的清单 + 原图 + 缩略图。
//
// 构建期（tools/EllenWikiSync/sync_assets.py）把 A4 清单与图片拷进安装包，布局：
//   assets/assets-manifest.json              上游清单原样（含 license 块）
//   assets/orig/<id>/<file>                  原图即缩略尺寸的条目（icon / 技能图标等，约 3747 个）
//   assets/thumbs/<id>/thumb-<stem>.webp     大图的 256px 预览（759 张）
//
// 运行时只做两件事：按 key 找本地文件、把 license.attribution 交给 UI 展示。
// **不重新编码、不校验 sha**——随包文件由构建脚本按清单 sha 校验过，运行时再算 759 次 sha 只会拖慢启动。

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ellen.Launcher.Domain.WikiSource;

/// <summary>随包敌人图片包。缺目录/缺清单时退化为「没有随包图片」，由调用方走缓存/在线，不抛异常。</summary>
public sealed class EnemyAssetsBundle
{
    public const string ManifestFileName = "assets-manifest.json";
    public const string OriginalsDirName = "orig";
    public const string ThumbsDirName = "thumbs";

    private readonly Dictionary<string, string> originals;
    private readonly Dictionary<string, string> thumbs;

    private EnemyAssetsBundle(
        string root,
        WikiAssetManifest? manifest,
        Dictionary<string, string> originals,
        Dictionary<string, string> thumbs,
        long bytes)
    {
        Root = root;
        Manifest = manifest;
        this.originals = originals;
        this.thumbs = thumbs;
        Bytes = bytes;
    }

    /// <summary>空包：没有随包图片（未打包 / 目录缺失）。</summary>
    public static EnemyAssetsBundle Empty { get; } = new(string.Empty, null, [], [], 0);

    public string Root { get; }

    public WikiAssetManifest? Manifest { get; }

    public bool Present => Manifest is not null;

    /// <summary>随包原图数（= 清单里没有 thumb 的条目里实际拷进来的那些）。</summary>
    public int OriginalCount => originals.Count;

    /// <summary>随包缩略图数。</summary>
    public int ThumbCount => thumbs.Count;

    /// <summary>清单声明的缩略图数（用于和实际落盘数对账，不等就是构建期漏拷）。</summary>
    public int DeclaredThumbCount => Manifest?.DeclaredThumbCount ?? 0;

    public long Bytes { get; }

    /// <summary>许可块；缺失时为空，UI 显示「未声明」而不是编一段。</summary>
    public EnemyAssetLicenseDto? License => Manifest?.License;

    public string? Attribution => License?.Attribution;

    public bool Redistributable => License?.Redistributable == true;

    /// <summary>诊断页一行文案：随包 N 张（原图 x + 缩略 y）/ 声明 z / 大小。</summary>
    public string SummaryText => Present
        ? $"随包图片 {OriginalCount + ThumbCount} 张（原图 {OriginalCount} + 缩略图 {ThumbCount}，声明缩略图 {DeclaredThumbCount}）/ {Bytes / 1024.0 / 1024.0:F1} MiB"
        : "随包图片：未打包（走缓存/在线）";

    /// <summary>缩略图文件名约定，与 ellen-wiki 生成器一致：<c>thumb-&lt;去扩展名&gt;.webp</c>。</summary>
    public static string ThumbFileName(string file)
    {
        string stem = Path.GetFileNameWithoutExtension(file ?? string.Empty);
        return "thumb-" + stem + ".webp";
    }

    public bool TryResolveOriginal(string id, string file, out string path)
        => originals.TryGetValue(Key(id, file), out path!);

    public bool TryResolveThumb(string id, string file, out string path)
        => thumbs.TryGetValue(Key(id, file), out path!)
            /* 枚举兜底建的键是 "<id>/<stem>.*"（缩略图文件名还原不出原扩展名） */
            || thumbs.TryGetValue(TupleKey(id, Path.GetFileNameWithoutExtension(file ?? string.Empty)), out path!);

    private static string Key(string id, string file) => $"{id}/{file}";

    /// <summary>
    /// 加载随包目录。<paramref name="assetsRoot"/> 为空或不存在 → 返回 <see cref="Empty"/> 并记一条显式诊断。
    /// 清单损坏但图片在 → 退化为按目录枚举（仍可离线看图），同时记 <c>assets-bundle-manifest-invalid</c>。
    /// </summary>
    public static EnemyAssetsBundle Load(string? assetsRoot, WikiDiagnosticsLog? log = null)
    {
        WikiSourceStamp stamp = WikiSourceStamp.FromBundledWeb753("随包图片");
        if (string.IsNullOrWhiteSpace(assetsRoot))
        {
            return Empty;
        }

        if (!Directory.Exists(assetsRoot))
        {
            log?.Add("assets-bundle-missing", stamp, $"{assetsRoot} 不存在", "随包图片未打包");
            return new EnemyAssetsBundle(assetsRoot, null, [], [], 0);
        }

        WikiAssetManifest? manifest = WikiAssetManifest.LoadFile(Path.Combine(assetsRoot, ManifestFileName));
        Dictionary<string, string> originals = new(StringComparer.Ordinal);
        Dictionary<string, string> thumbs = new(StringComparer.Ordinal);
        long bytes = 0;

        string originalsRoot = Path.Combine(assetsRoot, OriginalsDirName);
        string thumbsRoot = Path.Combine(assetsRoot, ThumbsDirName);

        if (manifest is null)
        {
            log?.Add("assets-bundle-manifest-invalid", stamp,
                $"{Path.Combine(assetsRoot, ManifestFileName)} 缺失或不可解析，退化为按目录枚举", "随包清单不可用");
        }
        else
        {
            /* 一次目录遍历建立「相对路径 → 绝对路径」索引（只枚举，不 stat），体积取清单声明值。
               原先逐条 File.Exists + FileInfo.Length = 9000+ 次 stat（实测 215ms）；
               必须保留存在性校验：文件被删时应当回退在线 URL，而不是把坏路径交给图片控件。
               体积不能直接用 entry.Size：带 thumb 的条目在 orig/ 里根本没有文件，
               那些 size 是大图体积，累加会虚高（实测把 23.6 MiB 算成 200+ MiB）。
               sync_assets.py 已按 sha256 校验落盘内容，这里的枚举只是防「装完被删」。 */
            Dictionary<string, string> presentOriginals = IndexByRelativePath(originalsRoot);
            Dictionary<string, string> presentThumbs = IndexByRelativePath(thumbsRoot);

            foreach (EnemyAssetFileDto entry in manifest.Dto.Files)
            {
                if (presentOriginals.TryGetValue(entry.Key, out string? original))
                {
                    originals[entry.Key] = original;
                    bytes += entry.Size;
                }

                if (entry.Thumb is { File.Length: > 0 } thumb
                    && presentThumbs.TryGetValue($"{entry.Id}/{thumb.File}", out string? thumbPath))
                {
                    thumbs[entry.Key] = thumbPath;
                    bytes += thumb.Size;
                }
            }

            if (manifest.DeclaredThumbCount != thumbs.Count)
            {
                log?.Add("assets-bundle-thumb-drift", stamp,
                    $"清单声明 {manifest.DeclaredThumbCount} 张缩略图，随包实际 {thumbs.Count} 张",
                    "随包图片与清单不一致（构建期漏拷）");
            }

            if (manifest.License?.Redistributable != true)
            {
                // 理论上不该出现：构建脚本在 redistributable!=true 时拒绝拷贝。
                log?.Add("assets-bundle-license", stamp,
                    $"清单 license.redistributable={manifest.License?.Redistributable}，本安装包内不应存在随包图片",
                    "许可与随包状态不一致");
            }
        }

        /* 清单缺失/损坏时的兜底：按目录枚举。key 由 <id>/<file> 还原，
           缩略图文件名 thumb-<stem>.webp 反推不出原扩展名，统一映射到 "*.webp" 键上——
           只有原图确实是 webp 时才可能命中，宁可不命中也不猜错 key。 */
        if (originals.Count == 0 && thumbs.Count == 0)
        {
            foreach (string dir in SafeDirs(originalsRoot))
            {
                string id = Path.GetFileName(dir);
                foreach (string file in SafeFiles(dir, "*"))
                {
                    originals[$"{id}/{Path.GetFileName(file)}"] = file;
                    bytes += new FileInfo(file).Length;
                }
            }

            foreach (string dir in SafeDirs(thumbsRoot))
            {
                string id = Path.GetFileName(dir);
                foreach (string file in SafeFiles(dir, "*.webp"))
                {
                    string name = Path.GetFileName(file);
                    if (!name.StartsWith("thumb-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string stem = Path.GetFileNameWithoutExtension(name)["thumb-".Length..];
                    thumbs[TupleKey(id, stem)] = file;
                    bytes += new FileInfo(file).Length;
                }
            }
        }

        if (manifest is not null)
        {
            log?.Add("assets-bundle-loaded", stamp,
                $"随包图片 {originals.Count} 原图 + {thumbs.Count} 缩略图（{bytes / 1024.0 / 1024.0:F1} MiB）",
                null);
        }

        return new EnemyAssetsBundle(assetsRoot, manifest, originals, thumbs, bytes);
    }

    /* 枚举兜底用：thumb-<stem> 无法还原原扩展名，键记为 "<id>/<stem>.*"，
       查询时由 TryResolveThumb 用同一规则匹配（见下）。 */
    private const string AnyExtSuffix = ".*";

    private static string TupleKey(string id, string stem) => $"{id}/{stem}{AnyExtSuffix}";

    private static IEnumerable<string> SafeDirs(string root)
    {
        try
        {
            return Directory.Exists(root) ? Directory.EnumerateDirectories(root) : [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeFiles(string dir, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(dir, pattern);
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <summary>一次遍历整棵目录树，返回「&lt;id&gt;/&lt;file&gt; → 绝对路径」。失败返回空表。</summary>
    private static Dictionary<string, string> IndexByRelativePath(string root)
    {
        Dictionary<string, string> index = new(StringComparer.Ordinal);
        if (!Directory.Exists(root))
        {
            return index;
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                index[relative] = file;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return index;
    }
}
