// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data.Converter;
using Ellen.Launcher.Web.Endpoint.Upstream;

namespace Ellen.Launcher.Model.Metadata.Converter;

public sealed partial class QualityConverter : ValueConverter<QualityType, Uri>
{
    public override Uri Convert(QualityType from)
    {
        string name = Enum.GetName(from) ?? from.ToString();
        if (name is nameof(QualityType.QUALITY_ORANGE_SP))
        {
            name = "QUALITY_RED";
        }

        return StaticResourcesEndpoints.StaticRaw("Bg", $"UI_{name}.png").ToUri();
    }
}