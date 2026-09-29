// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Windows.Storage;

namespace Ellen.Launcher.Core.Diagnostics;

[SuppressMessage("", "SH001", Justification = "IEllenDiagnostics must be public in order to be exposed to the scripting environment")]
public interface IEllenDiagnostics
{
    DiagnosticsSettingsProvider LocalSettings { get; }

    ValueTask<int> ExecuteSqlAsync(string sql);

    ApplicationDataCompositeValue MakeApplicationDataCompositeValue(params string[] keys);
}
