// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.UI.Xaml.Data.Converter.Specialized;

public sealed partial class HardChallengeDifficultyIconConverter : ValueConverter<HardChallengeDifficultyLevel, Uri>
{
    public static Uri Convert(string iconName)
    {
        return InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/Icon/{iconName}.png");
    }

    public override Uri Convert(HardChallengeDifficultyLevel from)
    {
        return InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/Icon/UI_LeyLineChallenge_Medal_{from:D}.png");
    }
}