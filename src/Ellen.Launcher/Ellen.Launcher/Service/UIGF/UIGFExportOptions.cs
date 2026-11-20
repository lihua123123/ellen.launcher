// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.UIGF;

public sealed class UIGFExportOptions
{
    public required string FilePath { get; set; }

    public required ImmutableArray<(GachaGameType Game, ulong Uid)> Accounts { get; set; }

    public UIGFVersion Version { get; set; } = UIGFVersion.UIGF42;
}
