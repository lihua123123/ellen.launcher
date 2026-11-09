// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Caching;
using Ellen.Launcher.Core.DataTransfer;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.Logging;
using Ellen.Launcher.UI.Content;
using Ellen.Launcher.UI.Xaml.Control.Theme;
using Ellen.Launcher.UI.Xaml.Media.Animation;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Ellen.Launcher.UI.Xaml.Control.Image;

[SuppressMessage("", "CA1001")]
[SuppressMessage("", "SH003")]
[TemplatePart(Name = PartImage, Type = typeof(Microsoft.UI.Xaml.Controls.Image))]
[DependencyProperty<string>("SourceName", DefaultValue = "UnknownSourceName")]
[DependencyProperty<string>("CachedName", DefaultValue = "UnknownCachedName")]
[DependencyProperty<Thickness>("NineGrid", NotNull = true)]
[DependencyProperty<Stretch>("Stretch", DefaultValue = Stretch.Uniform, NotNull = true)]
[DependencyProperty<object>("PlaceholderSource")]
[DependencyProperty<object>("Source", PropertyChangedCallbackName = nameof(OnSourceChanged))]
[DependencyProperty<bool>("ShowAsMonoChrome", DefaultValue = false, NotNull = true)]
[DependencyProperty<double>("DecodeWidth", DefaultValue = 0D, NotNull = true)]

/// <summary>
/// 图片出现时是否做 200ms 淡入。默认 true（列表滚动时观感更柔和）。
/// 详情页那些「每次切换条目都会重建」的图片（技能图标、一图流）要设 false：
/// 重建后整组图从透明淡入，看起来就是点开敌人闪一下。
/// </summary>
[DependencyProperty<bool>("FadeIn", DefaultValue = true, NotNull = true)]
public sealed partial class CachedImage : Microsoft.UI.Xaml.Controls.Control
{
    private const string PartImage = "Image";

    private static readonly ConditionalWeakTable<Microsoft.UI.Xaml.Controls.Image, object> IsPlaceholder = [];

    /// <summary>FadeIn=False 的 Image：换源前保留旧图、新图解码完直接不透明，避免「清空→淡入」的闪。</summary>
    private static readonly ConditionalWeakTable<Microsoft.UI.Xaml.Controls.Image, object> NoFadeImages = [];

    private readonly AsyncLock sourceLock = new();

    private CancellationTokenSource? sourceCts;

    public CachedImage()
    {
        DefaultStyleKey = typeof(CachedImage);
        Loaded += OnLoaded;
    }

    private Microsoft.UI.Xaml.Controls.Image? Image { get; set; }

    private bool IsInitialized { get; set; }

    protected override void OnApplyTemplate()
    {
        if (Image is { } oldImage)
        {
            oldImage.ImageOpened -= OnImageOpened;
            oldImage.ImageFailed -= OnImageFailed;
        }

        Image = GetTemplateChild(PartImage) as Microsoft.UI.Xaml.Controls.Image;

        if (Image is { } newImage)
        {
            newImage.ImageOpened += OnImageOpened;
            newImage.ImageFailed += OnImageFailed;
        }

        IsInitialized = true;
        SetSourceAsync(Source, false).SafeForget();

        base.OnApplyTemplate();
    }

    private static bool IsHttpUri(Uri uri)
    {
        return uri is { IsAbsoluteUri: true, Scheme: "http" or "https" };
    }

    private static Uri ResolveAppxUriForUnpackaged(Uri uri)
    {
        if (Core.RuntimeEnvironment.IsPackaged)
        {
            return uri;
        }

        return InstalledLocation.ToAbsoluteUri(uri);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CachedImage control)
        {
            return;
        }

        if (Equals(e.OldValue, e.NewValue))
        {
            return;
        }

        control.SetSourceAsync(e.NewValue, false).SafeForget();
    }

    private static void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (sender is not CachedImage control)
        {
            return;
        }

        control.SetSourceAsync(control.Source, false).SafeForget();
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not CachedImage control)
        {
            return;
        }

        control.ActualThemeChanged += OnActualThemeChanged;
        control.Unloaded += OnUnloaded;
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not CachedImage control)
        {
            return;
        }

        control.ActualThemeChanged -= OnActualThemeChanged;
        control.Loaded -= OnLoaded;
        control.Unloaded -= OnUnloaded;
    }

    private static void OnImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        // FadeIn=False 的直接全不透明：图片解码完就出现，不做 0→1 淡入。
        if (sender is Microsoft.UI.Xaml.Controls.Image image && NoFadeImages.TryGetValue(image, out _))
        {
            image.Opacity = 1;
            return;
        }

        AnimationBuilder
            .Create()
            .Opacity(
                to: 1D,
                duration: Constants.ImageOpacityFadeInOutFast,
                easingType: EasingType.Quartic,
                easingMode: EasingMode.EaseInOut)
            .Start(element);
    }

    private static void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is not Microsoft.UI.Xaml.Controls.Image image)
        {
            return;
        }

        if (IsPlaceholder.TryGetValue(image, out _))
        {
            return;
        }

        // P5a 灰度指标：只统计敌人图鉴图源（ellen.wiki 远程图 / Wiki 缓存本地文件）的解码失败，
        // 全应用其它图片的失败不计入，避免污染「图片解析失败率」。
        if (image.Source is BitmapImage bitmap
            && Ellen.Launcher.Service.EnemyArchive.WikiImageDecodeCounter.IsWikiImage(bitmap.UriSource))
        {
            Ellen.Launcher.Service.EnemyArchive.WikiImageDecodeCounter.Report();
        }

        AnimationBuilder
            .Create()
            .Opacity(
                to: 0,
                duration: Constants.ImageOpacityFadeInOutFast,
                easingType: EasingType.Quartic,
                easingMode: EasingMode.EaseInOut)
            .Start(image);
    }

    private static async Task ImageAttachUriSourceWithAnimationAsync(Microsoft.UI.Xaml.Controls.Image? target, Uri? uri, bool placeholder, int decodeLength)
    {
        if (target is null)
        {
            return;
        }

        if (placeholder)
        {
            IsPlaceholder.Add(target, new());
        }
        else
        {
            IsPlaceholder.Remove(target);
        }

        if (uri is null)
        {
            await AnimationBuilder
                .Create()
                .Opacity(
                    to: 0D,
                    duration: Constants.ImageOpacityFadeInOutFast,
                    easingType: EasingType.Quartic,
                    easingMode: EasingMode.EaseInOut)
                .StartAsync(target)
                .ConfigureAwait(true);

            if (!target.IsLoaded)
            {
                return;
            }

            target.Visibility = Visibility.Collapsed;
            target.Source = null;
        }
        else
        {
            // https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/optimize-animations-and-media#optimize-image-resources
            // Connecting the BitmapImage to the tree before setting its UriSource
            target.Visibility = Visibility.Visible;

            // FadeIn=False：不透明度直接拉满，避免「先透明、再等解码、再淡入」的三段闪烁。
            if (NoFadeImages.TryGetValue(target, out _))
            {
                target.Opacity = 1;
            }

            BitmapImage source = new();

            // Right-sized decoding. Item lists realize hundreds of icons; decoding each one at its
            // native size (256px and up) instead of the size it is actually painted at is the main
            // reason long lists stutter while scrolling. Must be set before UriSource.
            // Only the width is set: BitmapImage scales the other axis proportionally, whereas
            // setting both would force a square bitmap and visibly squish non-square sources.
            if (decodeLength > 0)
            {
                source.DecodePixelType = DecodePixelType.Logical;
                source.DecodePixelWidth = decodeLength;
            }

            // https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/optimize-animations-and-media#right-sized-decoding
            // UriSource should be set after the Image is connected to live XAML tree
            target.Source = source;
            source.UriSource = uri;
        }
    }

    /// <summary>
    /// Decides the width (in logical pixels) the image should be decoded to.
    /// Returns 0 when the display size is unknown, in which case the native size is used.
    /// </summary>
    private int ResolveDecodeLength()
    {
        double length = DecodeWidth;

        if (!double.IsFinite(length) || length <= 0)
        {
            length = double.IsFinite(Width) && Width > 0
                ? Width
                : double.IsFinite(Height) && Height > 0
                    ? Height
                    : 0D;
        }

        // Fall back to the measured size for elements that take their size from a parent
        // (an icon stretching inside a card template), but only once layout has run.
        if (length <= 0 && double.IsFinite(ActualWidth) && ActualWidth > 0)
        {
            length = ActualWidth;
        }

        return double.IsFinite(length) && length > 0 ? (int)Math.Ceiling(length) : 0;
    }

    private async Task<Uri?> ProvideCachedResourceAsync(Uri imageUri, CancellationToken token)
    {
        if (XamlRoot.XamlContext()?.ServiceProvider.GetRequiredService<IImageCache>() is not { } imageCache)
        {
            return default;
        }

        try
        {
            EllenException.ThrowIf(string.IsNullOrEmpty(imageUri.Host), SH.ControlImageCachedImageInvalidResourceUri);
            ElementTheme theme = ShowAsMonoChrome ? ThemeHelper.ApplicationToElement(ThemeHelper.ElementToApplication(ActualTheme)) : ElementTheme.Default;
            string file = await imageCache.GetFileFromCacheAsync(imageUri, theme).ConfigureAwait(true);
            token.ThrowIfCancellationRequested();
            CachedName = Path.GetFileName(file);
            SourceName = Path.GetFileName(imageUri.ToString());
            return file.ToUri();
        }
        catch (OperationCanceledException)
        {
            // Ignored
            throw;
        }
        catch (publicImageCacheException)
        {
            // Ignored
            throw;
        }
        catch (COMException)
        {
            try
            {
                // The image is corrupted, remove it.
                imageCache.Remove(imageUri);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
            }

            return default;
        }
        catch (Exception ex)
        {
            SentrySdk.CaptureException(ex);
            return default;
        }
    }

    private async Task SetSourceAsync(object? source, bool placeholder)
    {
        try
        {
            if (!IsInitialized)
            {
                return;
            }

            if (sourceCts is not null)
            {
                await sourceCts.CancelAsync().ConfigureAwait(true);
                sourceCts.Dispose();
            }

            sourceCts = new();
            CancellationToken token = sourceCts.Token;

            try
            {
                using (await sourceLock.LockAsync().ConfigureAwait(true))
                {
                    /* FadeIn=True：先把旧图淡出清空（列表滚动时的常规观感）。
                       FadeIn=False：**不清空**，旧图留在原位，等新图解码完直接替换 —— 
                       否则每次换源都会「先白一下再淡入」，详情页切条目就会闪。 */
                    if (FadeIn)
                    {
                        if (Image is { } fadingImage)
                        {
                            NoFadeImages.Remove(fadingImage);
                        }

                        // Remove old ImageSource from tree
                        await ImageAttachUriSourceWithAnimationAsync(Image, default, placeholder, 0).ConfigureAwait(true);
                    }
                    else if (Image is { } instantImage)
                    {
                        NoFadeImages.AddOrUpdate(instantImage, string.Empty);
                    }

                    if (source is null)
                    {
                        // FadeIn=False 时上面没清空旧图，这里必须显式清掉，否则换了没有图的条目会留着上一张。
                        if (!FadeIn && Image is { } blankImage)
                        {
                            blankImage.Source = null;
                            blankImage.Visibility = Visibility.Collapsed;
                        }

                        return;
                    }

                    if (source as Uri is not { } uri)
                    {
                        if (!Uri.TryCreate(source as string ?? source.ToString(), UriKind.RelativeOrAbsolute, out uri))
                        {
                            await SetSourceAsync(PlaceholderSource, true).ConfigureAwait(true);
                            return;
                        }
                    }

                    if (!IsHttpUri(uri) && !uri.IsAbsoluteUri)
                    {
                        uri = new("ms-appx:///" + uri.OriginalString.TrimStart('/'));
                    }

                    int decodeLength = ResolveDecodeLength();
                    /* 本地文件（随包图片 / 内容寻址缓存）已经是磁盘上的成品，不能再走「下载→缓存」：
                       file:// 的 Host 为空，ProvideCachedResourceAsync 会判成非法 URI 直接降级占位图，
                       表现为「缓存命中率 100% 但界面全是空白图」。 */
                    Uri? targetUri = uri.Scheme switch
                    {
                        "ms-appx" => ResolveAppxUriForUnpackaged(uri),
                        "file" => uri,
                        _ => await ProvideCachedResourceAsync(uri, token).ConfigureAwait(true),
                    };
                    if (!token.IsCancellationRequested)
                    {
                        // Only attach our image if we still have a valid request.
                        await ImageAttachUriSourceWithAnimationAsync(Image, targetUri, placeholder, decodeLength).ConfigureAwait(true);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored
            }
            catch
            {
                if (!IsLoaded)
                {
                    return;
                }

                if (!placeholder)
                {
                    await SetSourceAsync(PlaceholderSource, true).ConfigureAwait(true);
                }
            }
        }
        catch (COMException ex)
        {
            if (ex.HResult != unchecked((int)0x8000FFFF))
            {
                throw;
            }
        }
    }

    [Command("CopyToClipboardCommand")]
    private async Task CopyToClipboard()
    {
        SentrySdk.AddBreadcrumb(BreadcrumbFactory2.CreateUI("Copy image to Clipboard", "CachedImage.Command", [("source_name", SourceName ?? "<null>")]));

        if (XamlRoot.XamlContext()?.ServiceProvider.GetRequiredService<IClipboardProvider>() is not { } clipboardProvider)
        {
            return;
        }

        if (Image is { Source: BitmapImage bitmap })
        {
            using (FileStream netStream = File.OpenRead(bitmap.UriSource.LocalPath))
            {
                using (IRandomAccessStream fxStream = netStream.AsRandomAccessStream())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fxStream);
                    SoftwareBitmap softwareBitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    using (InMemoryRandomAccessStream memory = new())
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, memory);
                        encoder.SetSoftwareBitmap(softwareBitmap);
                        await encoder.FlushAsync();
                        await clipboardProvider.SetBitmapAsync(memory).ConfigureAwait(false);
                    }
                }
            }
        }
    }
}