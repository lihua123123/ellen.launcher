// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data.Converter;
using Ellen.Launcher.Web.Endpoint.Upstream;

namespace Ellen.Launcher.Model.Metadata.Converter;

public sealed partial class SkillIconConverter : ValueConverter<string, Uri>, IIconNameToUriConverter
{
    public static Uri IconNameToUri(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return StaticResourcesEndpoints.UIIconNone;
        }

        return name.StartsWith("UI_Talent_", StringComparison.Ordinal)
            ? StaticResourcesEndpoints.StaticRaw("Talent", $"{name}.png").ToUri()
            : StaticResourcesEndpoints.StaticRaw("Skill", $"{name}.png").ToUri();
    }

    public override Uri Convert(string from)
    {
        return IconNameToUri(from);
    }
}