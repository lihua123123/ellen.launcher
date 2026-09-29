// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.UIGF;

public sealed class UIGFImportOptions
{
    public required Model.InterChange.GachaLog.UIGF4 UIGF { get; set; }

    public required HashSet<uint> GachaArchiveUids { get; set; }
}