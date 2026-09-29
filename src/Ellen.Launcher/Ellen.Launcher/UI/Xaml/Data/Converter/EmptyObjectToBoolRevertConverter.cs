// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.WinUI.Converters;

namespace Ellen.Launcher.UI.Xaml.Data.Converter;

public sealed partial class EmptyObjectToBoolRevertConverter : EmptyObjectToObjectConverter
{
    public EmptyObjectToBoolRevertConverter()
    {
        EmptyValue = true;
        NotEmptyValue = false;
    }
}