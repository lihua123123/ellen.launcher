// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.InterChange.GachaLog;

public sealed class UIGF3Info
{
    [JsonPropertyName("uid")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public uint Uid { get; init; }

    [JsonPropertyName("lang")]
    public string? Lang { get; init; }

    [JsonPropertyName("export_timestamp")]
    public long ExportTimestamp { get; init; }

    [JsonPropertyName("export_time")]
    public string? ExportTime { get; init; }

    [JsonPropertyName("export_app")]
    public string? ExportApp { get; init; }

    [JsonPropertyName("export_app_version")]
    public string? ExportAppVersion { get; init; }

    [JsonPropertyName("uigf_version")]
    public string UigfVersion { get; init; } = default!;

    [JsonPropertyName("region_time_zone")]
    public int RegionTimeZone { get; init; }
}
