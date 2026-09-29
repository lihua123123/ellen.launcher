// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.BackgroundImage;

[ExtendedEnum]
public enum BackgroundImageType
{
    [LocalizationKey(nameof(SH.ServiceBackgroundImageTypeNone))]
    None,

    [LocalizationKey(nameof(SH.ServiceBackgroundImageTypeLocalFolder))]
    LocalFolder,

    [LocalizationKey(nameof(SH.ServiceBackgroundImageTypeBing))]
    EllenBing,

    [LocalizationKey(nameof(SH.ServiceBackgroundImageTypeDaily))]
    EllenDaily,

    [LocalizationKey(nameof(SH.ServiceBackgroundImageTypeLauncher))]
    EllenOfficialLauncher,
}