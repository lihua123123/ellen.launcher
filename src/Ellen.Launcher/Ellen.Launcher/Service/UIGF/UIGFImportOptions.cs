// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Service.UIGF;

public sealed class UIGFImportOptions
{
    public required Model.InterChange.GachaLog.UIGF4 UIGF { get; set; }

    public required HashSet<(GachaGameType Game, ulong Uid)> Accounts { get; set; }
}