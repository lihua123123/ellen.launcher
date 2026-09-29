// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;

namespace Ellen.Launcher.UI.Xaml.Data.Converter.Specialized;

[DependencyProperty<object>("ListValue")]
[DependencyProperty<object>("GridValue")]
public sealed partial class LayoutSwitchModeConverter : DependencyValueConverter<string, object?>
{
    public override object? Convert(string from)
    {
        return from switch
        {
            LayoutSwitch.List => ListValue,
            LayoutSwitch.Grid => GridValue,
            _ => default,
        };
    }
}