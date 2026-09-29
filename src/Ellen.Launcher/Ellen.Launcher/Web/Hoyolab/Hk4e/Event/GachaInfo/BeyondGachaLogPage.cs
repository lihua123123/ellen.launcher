using Ellen.Launcher.Core;
using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

public sealed class BeyondGachaLogPage : IJsonOnDeserialized
{
    [Obsolete("总是为 0")]
    [JsonPropertyName("total")]
    public string Total { get; set; } = default!;

    [JsonPropertyName("list")]
    public ImmutableArray<BeyondGachaLogItem> List { get; set; } = default!;

    public void OnDeserialized()
    {
        foreach (ref readonly BeyondGachaLogItem item in List.AsSpan())
        {
            TimeSpan offset = PlayerUid.GetRegionTimeZoneUtcOffsetForRegion(item.Region);
            item.Time = UnsafeDateTimeOffset.AdjustOffsetOnly(item.Time, offset);
        }
    }
}
