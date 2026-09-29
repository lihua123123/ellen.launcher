// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Upstream;

public sealed class EllenPackageInformation
{
    [JsonPropertyName("version")]
    public Version Version { get; set; } = default!;

    [JsonPropertyName("validation")]
    public string Validation { get; set; } = default!;

    [JsonPropertyName("mirrors")]
    public List<EllenPackageMirror> Mirrors { get; set; } = default!;
}