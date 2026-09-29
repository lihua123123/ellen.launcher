// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.
using Microsoft.UI.Xaml.Markup;

namespace Ellen.Launcher.UI.Xaml.Markup;

[MarkupExtensionReturnType(ReturnType = typeof(double))]
public sealed partial class DoubleExtension : MarkupExtension
{
    public string Value { get; set; } = default!;

    protected override object ProvideValue()
    {
        return XamlBindingHelper.ConvertValue(typeof(double), Value);
    }
}