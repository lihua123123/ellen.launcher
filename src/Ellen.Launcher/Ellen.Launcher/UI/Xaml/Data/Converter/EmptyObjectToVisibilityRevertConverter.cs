// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.WinUI.Converters;
using Microsoft.UI.Xaml;

namespace Ellen.Launcher.UI.Xaml.Data.Converter;

public sealed partial class EmptyObjectToVisibilityRevertConverter : EmptyObjectToObjectConverter
{
    public EmptyObjectToVisibilityRevertConverter()
    {
        EmptyValue = Visibility.Visible;
        NotEmptyValue = Visibility.Collapsed;
    }
}
