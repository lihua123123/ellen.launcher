// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data.Converter;
using Microsoft.UI.Xaml.Media;

namespace Ellen.Launcher.Model.Metadata.Converter;

/// <summary>
/// 品质 → 前景画刷，用于没有图标素材时的文字着色（例如绝区零调频记录列表）
/// </summary>
public sealed partial class QualityToBrushConverter : ValueConverter<QualityType, Brush>
{
    public override Brush Convert(QualityType from)
    {
        return new SolidColorBrush(QualityColorConverter.QualityToColor(from));
    }
}
