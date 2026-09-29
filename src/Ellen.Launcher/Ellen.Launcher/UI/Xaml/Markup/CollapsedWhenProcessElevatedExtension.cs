// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Ellen.Launcher.Core;

namespace Ellen.Launcher.UI.Xaml.Markup;

[MarkupExtensionReturnType(ReturnType = typeof(Visibility))]
public sealed partial class CollapsedWhenProcessElevatedExtension : MarkupExtension
{
    protected override object ProvideValue()
    {
        return EllenRuntime.IsProcessElevated ? Visibility.Collapsed : Visibility.Visible;
    }
}