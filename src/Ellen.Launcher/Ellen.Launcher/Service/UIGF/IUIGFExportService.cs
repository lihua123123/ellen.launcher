// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.UIGF;

public interface IUIGFExportService
{
    ValueTask ExportAsync(UIGFExportOptions exportOptions, CancellationToken token);
}