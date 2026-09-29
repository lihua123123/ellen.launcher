// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public sealed class ActTowerDetail
{
    [JsonPropertyName("is_unlock")]
    public required bool IsUnlock { get; init; }

    [JsonPropertyName("max_star")]
    public required int MaxStar { get; init; }

    [JsonPropertyName("total_star")]
    public required int TotalStar { get; init; }

    [JsonIgnore]
    public string FormattedStar
    {
        get
        {
            if (!IsUnlock)
            {
                return SH.WebHoyolabGameRecordActCalendarTowerLocked;
            }

            return SH.FormatWebHoyolabGameRecordActCalendarTowerStar(MaxStar, TotalStar);
        }
    }

    [JsonPropertyName("has_data")]
    public required bool HasData { get; init; }
}