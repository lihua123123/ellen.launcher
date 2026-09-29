// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Core;

namespace Ellen.Launcher.UI.Xaml.Data.Converter.Specialized;

public sealed partial class ThirdPartyIconConverter : ValueConverter<string, BitmapIcon>
{
    public const string TwitterName = "X (Twitter)";

    public override BitmapIcon Convert(string from)
    {
        Uri uri = from switch
        {
            TwitterName => InstalledLocation.ToAbsoluteUri("ms-appx:///Resource/ThirdParty/Twitter.png"),
            _ => InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/ThirdParty/{from}.png"),
        };

        return new()
        {
            ShowAsMonochrome = false,
            UriSource = uri,
        };
    }
}