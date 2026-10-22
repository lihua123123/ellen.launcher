// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Nap.Event.GachaRecord;

/// <summary>
/// 绝区零调频记录分页响应，结构与原神一致，但区服字段是 prod_gf_* 系列，不能直接复用原神模型
/// </summary>
public sealed class ZZZGachaLogPage : IJsonOnDeserialized
{
    [JsonPropertyName("page")]
    public string Page { get; set; } = default!;

    [JsonPropertyName("size")]
    public string Size { get; set; } = default!;

    [JsonPropertyName("list")]
    public ImmutableArray<GachaLogItem> List { get; set; } = default!;

    [JsonPropertyName("region")]
    public string Region { get; set; } = default!;

    [JsonPropertyName("region_time_zone")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? RegionTimeZone { get; set; }

    public void OnDeserialized()
    {
        TimeSpan offset = RegionTimeZone is { } timeZone
            ? TimeSpan.FromHours(timeZone)
            : GetRegionTimeZoneUtcOffset(Region);

        foreach (ref readonly GachaLogItem item in List.AsSpan())
        {
            item.Time = UnsafeDateTimeOffset.AdjustOffsetOnly(item.Time, offset);
        }
    }

    /// <summary>
    /// 响应未携带时区时，按区服字符串推断
    /// </summary>
    private static TimeSpan GetRegionTimeZoneUtcOffset(string region)
    {
        return region switch
        {
            "prod_gf_us" => TimeSpan.FromHours(-5),
            "prod_gf_eu" => TimeSpan.FromHours(1),
            "prod_gf_jp" => TimeSpan.FromHours(9),
            "prod_gf_sg" => TimeSpan.FromHours(8),
            _ => TimeSpan.FromHours(8), // prod_gf_cn 与其他未知区服默认 +8
        };
    }
}
