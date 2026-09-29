// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Bridge;

[ExtendedEnum]
public enum BridgeShareSaveType
{
    [LocalizationKey(nameof(SH.WebBridgeShareSaveKindCopyToClipboard))]
    CopyToClipboard,

    [LocalizationKey(nameof(SH.WebBridgeShareSaveKindSaveAsFile))]
    SaveAsFile,
}