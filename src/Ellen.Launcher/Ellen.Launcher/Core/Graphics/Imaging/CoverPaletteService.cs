// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Caching;
using Ellen.Launcher.Win32.System.WinRT;
using System.Collections.Concurrent;
using System.IO;
using WinRT;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.UI;

namespace Ellen.Launcher.Core.Graphics.Imaging;

/// <summary>
/// 封面取色结果：<see cref="BarColor"/> 用来铺时间条，<see cref="TextColor"/> 是在它上面保证可读的文字色。
/// </summary>
public readonly record struct CoverPalette(Color BarColor, Color TextColor);

/// <summary>
/// 从公告封面图里取一个「最鲜艳」的主色，用于给首页公告时间条自动着色。
/// <para>
/// 参考站用 node-vibrant（MMCQ 中位切分）取色；这里刻意用更轻的做法：
/// 把图缩到 <see cref="SampleSize"/> 见方，按 4bit/通道量化成桶，
/// 以「像素数 × (1 + 饱和度权重)」给每个桶打分，取分数最高的桶求均值。
/// 采样点本来就只有一千来个，实测量级在一毫秒级，且结果稳定、不会像整图平均色那样发灰。
/// </para>
/// <para>
/// 结果按 URL 常驻缓存（<see cref="ConcurrentDictionary{TKey,TValue}"/> + 共享 Task），
/// 同一条公告反复重建时间轴不会重复解码。
/// </para>
/// </summary>
public static class CoverPaletteService
{
    /// <summary>量化前的缩略边长（像素）。颜色统计用不着原图分辨率。</summary>
    private const int SampleSize = 32;

    /// <summary>4bit/通道 → 16³ 个桶。</summary>
    private const int BucketCount = 1 << 12;

    /// <summary>低于这个亮度视为「近黑」（大面积黑边、深色描边），不参与取色。</summary>
    private const int MinChannelMax = 32;

    /// <summary>
    /// 第一遍统计要求的最低饱和度。宣传图大面积是白底黑字，
    /// 只按面积投票会选到米白；先限定「有颜色」的像素再投票更接近人眼认知的主色。
    /// </summary>
    private const double MinSaturation = 0.18d;

    private static readonly ConcurrentDictionary<string, Task<CoverPalette?>> PaletteCache = new(StringComparer.Ordinal);

    /// <summary>
    /// 取 <paramref name="uri"/> 这张封面的主色。图片走 <see cref="IImageCache"/> 的本地缓存文件，
    /// 与 <c>CachedImage</c> 共用一份缓存，不会产生第二次下载。
    /// </summary>
    /// <returns>取色失败（图不存在 / 解码失败 / 全黑全白）时返回 <see langword="null"/>。</returns>
    public static Task<CoverPalette?> GetAsync(Uri uri, IImageCache imageCache)
    {
        return PaletteCache.GetOrAdd(uri.OriginalString, _ => LoadAsync(uri, imageCache));
    }

    private static async Task<CoverPalette?> LoadAsync(Uri uri, IImageCache imageCache)
    {
        try
        {
            string path = await imageCache.GetFileFromCacheAsync(uri).ConfigureAwait(false);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            using (FileStream stream = File.OpenRead(path))
            {
                return await ExtractAsync(stream).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            // 取色是纯装饰：任何失败都退回调用方的兜底色，不打扰用户。
            return null;
        }
    }

    private static async Task<CoverPalette?> ExtractAsync(Stream stream)
    {
        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream.AsRandomAccessStream());

        // 缩略解码：既省时间，也顺带把噪点抹平（等价于参考站取色前的降采样）。
        BitmapTransform transform = new()
        {
            ScaledWidth = SampleSize,
            ScaledHeight = SampleSize,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        return Analyze(bitmap);
    }

    private static CoverPalette? Analyze(SoftwareBitmap bitmap)
    {
        double[] weights = new double[BucketCount];
        double[] sumR = new double[BucketCount];
        double[] sumG = new double[BucketCount];
        double[] sumB = new double[BucketCount];

        using (BitmapBuffer buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read))
        {
            using (IMemoryBufferReference reference = buffer.CreateReference())
            {
                reference.As<IMemoryBufferByteAccess>().GetBuffer(out Span<byte> bytes);

                // 第一遍只看有颜色的像素：白底/浅灰底的大面积留白会把主色带偏成米白。
                bool found = TryFindDominant(bytes, MinSaturation, weights, sumR, sumG, sumB, out double averageR, out double averageG, out double averageB);

                if (!found)
                {
                    // 整张图几乎没有彩色（纯灰度封面）时，退化成取面积最大的颜色。
                    Array.Clear(weights);
                    Array.Clear(sumR);
                    Array.Clear(sumG);
                    Array.Clear(sumB);

                    found = TryFindDominant(bytes, 0d, weights, sumR, sumG, sumB, out averageR, out averageG, out averageB);
                }

                if (!found)
                {
                    return null;
                }

                (double h, double s, double v) = ToHsv(averageR, averageG, averageB);

                // 把明度夹到「能当底色、又能压住白字/黑字」的区间：
                // 太暗看不出色相，太亮会把封面渐变冲淡。
                v = Math.Clamp(v, 0.32d, 0.78d);
                s = Math.Clamp(s, 0.25d, 0.95d);

                Color barColor = FromHsv(h, s, v);
                Color textColor = GetRelativeLuminance(barColor) > 0.58d
                    ? Color.FromArgb(0xFF, 0x1F, 0x1F, 0x1F)
                    : Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

                return new(barColor, textColor);
            }
        }
    }

    /// <summary>
    /// 单遍统计：按 4bit/通道分桶，取权重最高的桶求平均色。
    /// </summary>
    private static bool TryFindDominant(
        ReadOnlySpan<byte> bytes,
        double minSaturation,
        double[] weights,
        double[] sumR,
        double[] sumG,
        double[] sumB,
        out double averageR,
        out double averageG,
        out double averageB)
    {
        averageR = 0d;
        averageG = 0d;
        averageB = 0d;

        for (int i = 0; i + 3 < bytes.Length; i += 4)
        {
            byte b = bytes[i];
            byte g = bytes[i + 1];
            byte r = bytes[i + 2];
            byte a = bytes[i + 3];

            if (a < 125)
            {
                continue;
            }

            int max = Math.Max(r, Math.Max(g, b));
            int min = Math.Min(r, Math.Min(g, b));

            // 近黑（描边、暗角）与近白（留白、对话框底）对「主色」没有贡献。
            if (max < MinChannelMax || (r > 250 && g > 250 && b > 250))
            {
                continue;
            }

            double saturation = (double)(max - min) / max;
            if (saturation < minSaturation)
            {
                continue;
            }

            // 饱和度越高越可能是画面的视觉重心，权重随之放大。
            double weight = 1d + (2d * saturation);

            int bucket = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
            weights[bucket] += weight;
            sumR[bucket] += r * weight;
            sumG[bucket] += g * weight;
            sumB[bucket] += b * weight;
        }

        int best = -1;
        for (int i = 0; i < BucketCount; i++)
        {
            if (weights[i] > 0 && (best < 0 || weights[i] > weights[best]))
            {
                best = i;
            }
        }

        if (best < 0)
        {
            return false;
        }

        double total = weights[best];
        averageR = sumR[best] / total;
        averageG = sumG[best] / total;
        averageB = sumB[best] / total;
        return true;
    }

    /// <summary>WCAG 相对亮度（sRGB 线性化），用来决定条上的文字用黑还是白。</summary>
    private static double GetRelativeLuminance(Color color)
    {
        static double Linearize(byte channel)
        {
            double value = channel / 255d;
            return value <= 0.03928d ? value / 12.92d : Math.Pow((value + 0.055d) / 1.055d, 2.4d);
        }

        return (0.2126d * Linearize(color.R)) + (0.7152d * Linearize(color.G)) + (0.0722d * Linearize(color.B));
    }

    private static (double H, double S, double V) ToHsv(double r, double g, double b)
    {
        double rf = r / 255d;
        double gf = g / 255d;
        double bf = b / 255d;

        double max = Math.Max(rf, Math.Max(gf, bf));
        double min = Math.Min(rf, Math.Min(gf, bf));
        double delta = max - min;

        double h = 0d;
        if (delta > 0d)
        {
            if (max == rf)
            {
                h = 60d * (((gf - bf) / delta) % 6d);
            }
            else if (max == gf)
            {
                h = 60d * (((bf - rf) / delta) + 2d);
            }
            else
            {
                h = 60d * (((rf - gf) / delta) + 4d);
            }
        }

        if (h < 0d)
        {
            h += 360d;
        }

        double s = max <= 0d ? 0d : delta / max;
        return (h, s, max);
    }

    private static Color FromHsv(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1d - Math.Abs(((h / 60d) % 2d) - 1d));
        double m = v - c;

        (double r, double g, double b) = h switch
        {
            < 60d => (c, x, 0d),
            < 120d => (x, c, 0d),
            < 180d => (0d, c, x),
            < 240d => (0d, x, c),
            < 300d => (x, 0d, c),
            _ => (c, 0d, x),
        };

        return Color.FromArgb(
            0xFF,
            (byte)Math.Round((r + m) * 255d),
            (byte)Math.Round((g + m) * 255d),
            (byte)Math.Round((b + m) * 255d));
    }
}
