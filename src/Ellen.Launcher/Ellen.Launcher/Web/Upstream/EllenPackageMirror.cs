// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Text.Json.Annotation;

namespace Ellen.Launcher.Web.Upstream;

public sealed class EllenPackageMirror
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    [JsonPropertyName("mirror_name")]
    public string MirrorName { get; set; } = default!;

    [JsonEnumHandling(JsonEnumHandling.String)]
    [JsonPropertyName("mirror_type")]
    public EllenPackageMirrorType MirrorType { get; set; }
}
