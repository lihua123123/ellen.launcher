// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Ellen.Launcher.UI.Xaml.Data.Converter;

/// <summary>
/// 元素配色键 → 画刷（配色表见 <see cref="ElementPalette"/>）。
/// <para>键无法识别时返回 <see cref="DependencyProperty.UnsetValue"/>，由目标属性回落默认前景色。</para>
/// </summary>
public sealed partial class ElementKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is string key && ElementPalette.TryGetBrush(key, out Brush? brush)
            ? brush
            : DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return DependencyProperty.UnsetValue;
    }
}
