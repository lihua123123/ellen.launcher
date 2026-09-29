// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data.Converter;

namespace Ellen.Launcher.Model.Metadata.Converter;

[DependencyProperty<ImageSource>("RedSource")]
[DependencyProperty<ImageSource>("OrangeSource")]
[DependencyProperty<ImageSource>("PurpleSource")]
[DependencyProperty<ImageSource>("BlueSource")]
[DependencyProperty<ImageSource>("GreenSource")]
[DependencyProperty<ImageSource>("WhiteSource")]
[DependencyProperty<ImageSource>("NoneSource")]
public sealed partial class QualityToImageSourceConverter : DependencyValueConverter<QualityType, ImageSource?>
{
    public QualityToImageSourceConverter()
    {
        if (RuntimeEnvironment.IsUnpackaged)
        {
            // In unpackaged mode, ms-appx:/// URIs in XAML BitmapImage don't resolve.
            // Override with absolute file URIs.
            RedSource = new BitmapImage(InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ItemIcon/UI_QUALITY_RED.png"));
            OrangeSource = new BitmapImage(InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ItemIcon/UI_QUALITY_ORANGE.png"));
            PurpleSource = new BitmapImage(InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ItemIcon/UI_QUALITY_PURPLE.png"));
            BlueSource = new BitmapImage(InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ItemIcon/UI_QUALITY_BLUE.png"));
            GreenSource = new BitmapImage(InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ItemIcon/UI_QUALITY_GREEN.png"));
            WhiteSource = new BitmapImage(InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ItemIcon/UI_QUALITY_WHITE.png"));
            NoneSource = new BitmapImage(InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ItemIcon/UI_QUALITY_NONE.png"));
        }
    }

    public override ImageSource? Convert(QualityType from)
    {
        return from switch
        {
            QualityType.QUALITY_ORANGE_SP => RedSource,
            QualityType.QUALITY_ORANGE => OrangeSource,
            QualityType.QUALITY_PURPLE => PurpleSource,
            QualityType.QUALITY_BLUE => BlueSource,
            QualityType.QUALITY_GREEN => GreenSource,
            QualityType.QUALITY_WHITE => WhiteSource,
            _ => NoneSource,
        };
    }
}
