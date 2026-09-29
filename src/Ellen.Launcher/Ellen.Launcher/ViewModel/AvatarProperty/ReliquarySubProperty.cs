// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public class ReliquarySubProperty
{
    public ReliquarySubProperty(FightProperty type, string value)
    {
        Name = type.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture);
        Value = value;
    }

    public string? Name { get; }

    public string Value { get; }
}