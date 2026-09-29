// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.UI.Xaml.Data.Converter.Specialized;

public sealed partial class RoleCombatDifficultyIconConverter : ValueConverter<RoleCombatDifficultyLevel, Uri>
{
    public override Uri Convert(RoleCombatDifficultyLevel from)
    {
        return InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/Icon/UI_RoleCombat_Medal_S_{from:D}.png");
    }
}