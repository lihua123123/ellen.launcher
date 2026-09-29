// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.UIGF;

[Service(ServiceLifetime.Transient, typeof(IUIGFExportService), Key = UIGFVersion.UIGF24)]
public sealed partial class UIGF24ExportService : AbstractUIGF3ExportService
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UIGF24ExportService(IServiceProvider serviceProvider);

    protected override string Version { get; } = "v2.4";
}
