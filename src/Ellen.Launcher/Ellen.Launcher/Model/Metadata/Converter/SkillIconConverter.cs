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

        // 米游社的命座图标会直接给出完整地址，原样使用，不要再拼静态资源前缀
        if (Uri.TryCreate(name, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri;
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