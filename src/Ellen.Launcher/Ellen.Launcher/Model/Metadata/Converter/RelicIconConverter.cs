// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data.Converter;
using Ellen.Launcher.Web.Endpoint.Upstream;

namespace Ellen.Launcher.Model.Metadata.Converter;

public sealed partial class RelicIconConverter : ValueConverter<string, Uri>, IIconNameToUriConverter
{
    public static Uri IconNameToUri(string name)
    {
        return StaticResourcesEndpoints.StaticRaw("RelicIcon", $"{name}.png").ToUri();
    }

    public override Uri Convert(string from)
    {
        return IconNameToUri(from);
    }
}