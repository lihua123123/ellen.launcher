// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.UIGF;

[Service(ServiceLifetime.Transient, typeof(IUIGFExportService), Key = UIGFVersion.UIGF30)]
public sealed partial class UIGF30ExportService : AbstractUIGF3ExportService
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UIGF30ExportService(IServiceProvider serviceProvider);

    protected override string Version { get; } = "v3.0";
}
