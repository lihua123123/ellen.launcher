// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata.Converter;
using System.Globalization;

namespace Ellen.Launcher.UI.Xaml.Data.Converter.Specialized;

public sealed partial class ElementTypeIconConverter : ValueConverter<ElementType, Uri?>
{
    public override Uri? Convert(ElementType from)
    {
        string? name = from.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture);
        return string.IsNullOrEmpty(name) ? default : ElementNameIconConverter.ElementNameToUri(name);
    }
}