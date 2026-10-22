// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Windows.Storage.Streams;

namespace Ellen.Launcher.Service.Game.QuickLaunch;

/// <summary>
/// 游戏图标提取：直接取游戏可执行文件自带的图标（和官方启动器、任务栏里看到的一致），
/// 避免自绘/占位图标与原图标不一致。
/// </summary>
public static class GameIconProvider
{
    /// <summary>
    /// 提取 exe 的关联图标并编码为 PNG。可在后台线程调用。
    /// </summary>
    public static byte[]? TryGetPngBytes(string? executablePath)
    {
        if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        try
        {
            using Icon? icon = Icon.ExtractAssociatedIcon(executablePath);
            if (icon is null)
            {
                return null;
            }

            using Bitmap bitmap = icon.ToBitmap();
            using MemoryStream stream = new();
            bitmap.Save(stream, ImageFormat.Png);
            return stream.ToArray();
        }
        catch (Exception)
        {
            // 图标资源损坏 / 文件被占用时退回内置图标
            return null;
        }
    }

    /// <summary>
    /// 把 PNG 字节包装成可在 XAML 中使用的图像源。必须在 UI 线程调用。
    /// </summary>
    public static async ValueTask<ImageSource> CreateImageSourceAsync(byte[] pngBytes)
    {
        InMemoryRandomAccessStream stream = new();
        using (DataWriter writer = new(stream))
        {
            writer.WriteBytes(pngBytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);

        BitmapImage image = new();
        await image.SetSourceAsync(stream);
        return image;
    }

    /// <summary>
    /// 随包的占位图标（<c>ms-appx:///</c>），仅在游戏未安装或图标提取失败时使用。必须在 UI 线程调用。
    /// </summary>
    public static ImageSource CreateBundledIcon(string msAppxUri)
    {
        return new BitmapImage(new Uri(msAppxUri));
    }
}
