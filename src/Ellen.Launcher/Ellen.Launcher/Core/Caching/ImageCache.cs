// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.IO;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.Factory.Process;
using Ellen.Launcher.Web.Endpoint.Upstream;
using Ellen.Launcher.Win32;
using System.Collections.Frozen;
using System.Diagnostics;
using System.IO;
using ThemeFile = (Microsoft.UI.Xaml.ElementTheme, Ellen.Launcher.Core.IO.ValueFile);

namespace Ellen.Launcher.Core.Caching;

[Service(ServiceLifetime.Singleton, typeof(IImageCache))]
public sealed partial class ImageCache : IImageCache
{
    private static readonly FrozenSet<string> SupportedSchemes =
    [
        Uri.UriSchemeHttp,
        Uri.UriSchemeHttps,
    ];

    private readonly AsyncKeyedLock<ThemeFile> themeFileLocks = new();
    private readonly AsyncKeyedLock<string> downloadLocks = new();

    private readonly IImageCacheDownloadOperation downloadOperation;

    [GeneratedConstructor]
    public partial ImageCache(IServiceProvider serviceProvider);

    private string CacheFolder
    {
        get => LazyInitializer.EnsureInitialized(ref field, static () =>
        {
            try
            {
                string folder = EllenRuntime.GetLocalCacheImageCacheDirectory();
                Directory.CreateDirectory(Path.Combine(folder, "Light"));
                Directory.CreateDirectory(Path.Combine(folder, "Dark"));
                return folder;
            }
            catch (Exception ex)
            {
                // 0x80070570 ERROR_FILE_CORRUPT
                EllenNative.Instance.ShowErrorMessage(ex.Message, ExceptionFormat.Format(ex));
                ProcessFactory.KillCurrent();
                return string.Empty;
            }
        });
    }

    public void Remove(Uri uriForCachedItem)
    {
        try
        {
            File.Delete(ImageCacheFile.GetHashedFile(CacheFolder, uriForCachedItem));
        }
        catch
        {
            // Ignored
        }
    }

    public ValueTask<ValueFile> GetFileFromCacheAsync(Uri uri)
    {
        return GetFileFromCacheAsync(uri, ElementTheme.Default);
    }

    public async ValueTask<ValueFile> GetFileFromCacheAsync(Uri uri, ElementTheme theme)
    {
        Debug.Assert(SupportedSchemes.Contains(uri.Scheme), "Unsupported URI scheme");

        ImageCacheFile imageCacheFile = ImageCacheFile.Create(CacheFolder, uri);
        string themedFileFullPath = imageCacheFile.GetThemedFile(theme);

        using (await themeFileLocks.LockAsync((theme, imageCacheFile.HashedFileName)).ConfigureAwait(false))
        {
            if (IsFileInvalid(themedFileFullPath))
            {
                using (await downloadLocks.LockAsync(imageCacheFile.HashedFileName).ConfigureAwait(false))
                {
                    if (IsFileInvalid(imageCacheFile.DefaultFilePath))
                    {
                        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateInfo("Begin to download file", "Core.Caching.ImageCache", [("Uri", uri.ToString()), ("File", imageCacheFile.DefaultFilePath)]));

                        try
                        {
                            await downloadOperation.DownloadFileAsync(uri, imageCacheFile.DefaultFilePath).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            Remove(uri);
                            throw;
                        }
                    }
                }
            }

            await EnsureThemedMonochromeFileExistsAsync(imageCacheFile, theme).ConfigureAwait(false);
            return themedFileFullPath;
        }
    }

    public ValueFile GetFileFromCategoryAndName(string category, string fileName)
    {
        return ImageCacheFile.Create(CacheFolder, StaticResourcesEndpoints.StaticRaw(category, fileName)).DefaultFilePath;
    }

    private static bool IsFileInvalid(string file, bool treatNullFileAsInvalid = true)
    {
        if (!File.Exists(file))
        {
            return treatNullFileAsInvalid;
        }

        try
        {
            long length = new FileInfo(file).Length;
            if (length == 0)
            {
                return true;
            }

            /* 只判「存在 + 长度非 0」是不够的：下载中途断流会留下一个 PNG 头完整、
               数据被截断的半截文件，它能一直命中缓存、永远不再重下，解码失败后界面就是空白
               （2026-11-10 武器图鉴「秘星典谕」立绘一直不显示就是这个原因）。
               这里按文件尾标志做一次轻量完整性校验，损坏就删掉让下载层重新拉。 */
            if (IsImageFileComplete(file, length))
            {
                return false;
            }

            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // 被占用也没关系：下载层用 File.Create 会直接覆盖
            }

            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// 校验常见图片格式的结束标志。只识别 PNG / JPEG / WebP / GIF；
    /// 其它格式一律放行（宁可漏判，也不能把正常缓存误删）。
    /// </summary>
    private static bool IsImageFileComplete(string file, long length)
    {
        const int Probe = 12;
        if (length < Probe * 2)
        {
            return true;
        }

        Span<byte> head = stackalloc byte[Probe];
        Span<byte> tail = stackalloc byte[Probe];

        using (FileStream stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            stream.ReadExactly(head);
            stream.Seek(-Probe, SeekOrigin.End);
            stream.ReadExactly(tail);
        }

        // PNG: 89 50 4E 47 0D 0A 1A 0A，末尾必须是 IEND chunk（长度 0 + "IEND" + CRC）
        if (head[0] is 0x89 && head[1] is 0x50 && head[2] is 0x4E && head[3] is 0x47)
        {
            return tail[4] is (byte)'I' && tail[5] is (byte)'E' && tail[6] is (byte)'N' && tail[7] is (byte)'D';
        }

        // JPEG: FF D8 ... FF D9
        if (head[0] is 0xFF && head[1] is 0xD8)
        {
            return tail[^2] is 0xFF && tail[^1] is 0xD9;
        }

        // WebP: "RIFF" + 4 字节小端长度 + "WEBP"；长度 = 文件长度 - 8（奇数时补 1 字节 padding）
        if (head[0] is (byte)'R' && head[1] is (byte)'I' && head[2] is (byte)'F' && head[3] is (byte)'F'
            && head[8] is (byte)'W' && head[9] is (byte)'E' && head[10] is (byte)'B' && head[11] is (byte)'P')
        {
            uint riffSize = (uint)(head[4] | (head[5] << 8) | (head[6] << 16) | (head[7] << 24));
            long expected = riffSize + 8L;
            return length == expected || length == expected + 1;
        }

        // GIF: "GIF8" ... 0x3B
        if (head[0] is (byte)'G' && head[1] is (byte)'I' && head[2] is (byte)'F' && head[3] is (byte)'8')
        {
            return tail[^1] is 0x3B;
        }

        return true;
    }

    private static async ValueTask EnsureThemedMonochromeFileExistsAsync(ImageCacheFile imageCacheFile, ElementTheme theme)
    {
        if (theme is ElementTheme.Default)
        {
            return;
        }

        try
        {
            using (FileStream sourceStream = File.OpenRead(imageCacheFile.DefaultFilePath))
            {
                using (FileStream themeStream = File.Create(imageCacheFile.GetThemedFile(theme)))
                {
                    await MonoChromeImageConverter.ConvertAndCopyToAsync(theme, sourceStream, themeStream).ConfigureAwait(false);
                }
            }
        }
        catch (IOException ex)
        {
            throw publicImageCacheException.Throw("Failed to convert image", ex);
        }
    }
}