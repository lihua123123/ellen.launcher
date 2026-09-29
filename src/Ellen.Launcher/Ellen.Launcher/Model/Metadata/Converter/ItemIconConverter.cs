// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data.Converter;
using Ellen.Launcher.Web.Endpoint.Upstream;

namespace Ellen.Launcher.Model.Metadata.Converter;

public sealed partial class ItemIconConverter : ValueConverter<string, Uri>, IIconNameToUriConverter
{
    public static Uri IconNameToUri(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return default!;
        }

        return name.StartsWith("UI_RelicIcon_", StringComparison.Ordinal)
            ? RelicIconConverter.IconNameToUri(name)
            : StaticResourcesEndpoints.StaticRaw("ItemIcon", $"{name}.png").ToUri();
    }

    public override Uri Convert(string from)
    {
        return IconNameToUri(from);
    }
}