// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Data;
using Ellen.Launcher.Service.UIGF;

namespace Ellen.Launcher.UI.Xaml.Data.Converter;

public sealed partial class UIGFVersionToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is UIGFVersion version)
        {
            return version switch
            {
                UIGFVersion.UIGF22 => "v2.2",
                UIGFVersion.UIGF23 => "v2.3",
                UIGFVersion.UIGF24 => "v2.4",
                UIGFVersion.UIGF30 => "v3.0",
                UIGFVersion.UIGF40 => "v4.0",
                UIGFVersion.UIGF41 => "v4.1",
                UIGFVersion.UIGF42 => "v4.2",
                _ => version.ToString(),
            };
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string str)
        {
            return str switch
            {
                "v2.2" => UIGFVersion.UIGF22,
                "v2.3" => UIGFVersion.UIGF23,
                "v2.4" => UIGFVersion.UIGF24,
                "v3.0" => UIGFVersion.UIGF30,
                "v4.0" => UIGFVersion.UIGF40,
                "v4.1" => UIGFVersion.UIGF41,
                "v4.2" => UIGFVersion.UIGF42,
                _ => UIGFVersion.None,
            };
        }

        return UIGFVersion.None;
    }
}
