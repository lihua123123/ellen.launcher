// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.ViewModel.Guide;

[ExtendedEnum]
public enum StaticResourceQuality
{
    [LocalizationKey(nameof(SH.ViewModelGuideStaticResourceQualityRaw))]
    Original,

    [LocalizationKey(nameof(SH.ViewModelGuideStaticResourceQualityHigh))]
    High,
}