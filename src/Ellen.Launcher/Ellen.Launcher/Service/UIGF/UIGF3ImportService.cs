// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.UIGF;

[Service(ServiceLifetime.Transient, typeof(IUIGFImportService), Key = UIGFVersion.UIGF30)]
public sealed partial class UIGF3ImportService : AbstractUIGF40ImportService
{
    [GeneratedConstructor(CallBaseConstructor = true)]
    public partial UIGF3ImportService(IServiceProvider serviceProvider);
}
