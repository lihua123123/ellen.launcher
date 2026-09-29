// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Ellen.Launcher.Core;

namespace Ellen.Launcher.UI.Xaml.Markup;

[MarkupExtensionReturnType(ReturnType = typeof(BitmapIcon))]
public sealed partial class BitmapIconExtension : MarkupExtension
{
    public Uri Source { get; set; } = default!;

    public bool ShowAsMonochrome { get; set; }

    protected override object ProvideValue()
    {
        Uri uri = RuntimeEnvironment.IsPackaged ? Source : InstalledLocation.ToAbsoluteUri(Source);

        return new BitmapIcon
        {
            ShowAsMonochrome = ShowAsMonochrome,
            UriSource = uri,
        };
    }
}