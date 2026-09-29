// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.UIGF;

[Service(ServiceLifetime.Transient, typeof(IUIGFExportService), Key = UIGFVersion.UIGF23)]
public sealed partial class UIGF23ExportService : AbstractUIGF3ExportService
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UIGF23ExportService(IServiceProvider serviceProvider);

    protected override string Version { get; } = "v2.3";
}
