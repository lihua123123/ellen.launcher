// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.DeprecatedFile;

public sealed class DeprecatedFileConfigurationsWrapper
{
    [JsonPropertyName("deprecated_file_configs")]
    public List<DeprecatedFilesWrapper> DeprecatedFileConfigurations { get; set; } = default!;
}
