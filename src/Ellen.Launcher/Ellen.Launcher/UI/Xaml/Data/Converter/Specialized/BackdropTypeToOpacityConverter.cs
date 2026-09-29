// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Media.Backdrop;

namespace Ellen.Launcher.UI.Xaml.Data.Converter.Specialized;

public sealed partial class BackdropTypeToOpacityConverter : ValueConverter<BackdropType, double>
{
    public override double Convert(BackdropType from)
    {
        return from is BackdropType.None ? 1 : 0;
    }
}