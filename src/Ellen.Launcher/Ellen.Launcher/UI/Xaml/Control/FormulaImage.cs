// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Formulas;
using System.IO;

namespace Ellen.Launcher.UI.Xaml.Control;

/// <summary>
/// 展示一个块公式的位图。
/// <para>
/// 公式不是运行时光栅化的：<c>tools/FormulasGen/gen.mjs</c> 已按亮 / 暗两套主题各预渲染一份 3× PNG
/// （<c>Resource/Formulas/Genshin/{light,dark}/{id}.png</c>），这里只按当前主题挑一张。
/// WinUI 3 没有数学排版能力，预渲染是本项目在不引入 WebView 的前提下唯一保真的做法。
/// </para>
/// <para>
/// 跟随 <see cref="FrameworkElement.ActualThemeChanged"/> 自动换图（与 <c>CachedImage</c> 同款做法）。
/// </para>
/// </summary>
[SuppressMessage("", "SH001")]
[DependencyProperty<FormulaBlock>("Block", PropertyChangedCallbackName = nameof(OnBlockChanged))]
public sealed partial class FormulaImage : ContentControl
{
    private const string ResourceRelativeFolder = "Resource/Formulas/Genshin";

    private readonly Microsoft.UI.Xaml.Controls.Image image = new()
    {
        Stretch = Stretch.Uniform,
    };

    public FormulaImage()
    {
        IsTabStop = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        HorizontalContentAlignment = HorizontalAlignment.Left;
        VerticalContentAlignment = VerticalAlignment.Top;
        Content = image;

        Loaded += OnLoaded;
    }

    private static void OnBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FormulaImage control)
        {
            control.Apply();
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FormulaImage control)
        {
            control.Loaded -= OnLoaded;
            control.ActualThemeChanged += OnActualThemeChanged;
            control.Unloaded += OnUnloaded;
            control.Apply();
        }
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FormulaImage control)
        {
            control.Unloaded -= OnUnloaded;
            control.ActualThemeChanged -= OnActualThemeChanged;
            control.Loaded += OnLoaded;
        }
    }

    private static void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (sender is FormulaImage control)
        {
            control.Apply();
        }
    }

    private void Apply()
    {
        if (Block is not { Image: { Length: > 0 } id } block)
        {
            image.Source = null;
            return;
        }

        image.Width = block.Width;
        image.Height = block.Height;

        string theme = ActualTheme == ElementTheme.Light ? "light" : "dark";
        string relativePath = $"{ResourceRelativeFolder}/{theme}/{id}.png";
        string absolutePath = InstalledLocation.GetAbsolutePath(relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(absolutePath))
        {
            image.Source = null;
            return;
        }

        image.Source = new BitmapImage(new Uri(absolutePath));
    }
}
