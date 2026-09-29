// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;

namespace Ellen.Launcher.Web.Hoyolab;

public readonly struct PlayerUid
{
    public readonly string Value;

    public readonly Region Region;

    public PlayerUid(string value, in Region? region = default)
    {
        EllenException.ThrowIfNot(HoyolabRegex.UidRegex.IsMatch(value), SH.WebHoyolabInvalidUid);
        Value = value;
        Region = region ?? Region.UnsafeFromUidString(value);
    }

    public static implicit operator PlayerUid(string source)
    {
        return FromUidString(source);
    }

    public static PlayerUid FromUidString(string uid)
    {
        return new(uid);
    }

    public static bool IsOversea(string uid)
    {
        EllenException.ThrowIfNot(HoyolabRegex.UidRegex.IsMatch(uid), SH.WebHoyolabInvalidUid);

        return uid.AsSpan()[^9] switch
        {
            >= '1' and <= '5' => false,
            _ => true,
        };
    }

    public static TimeSpan GetRegionTimeZoneUtcOffsetForUid(string uid)
    {
        EllenException.ThrowIfNot(HoyolabRegex.UidRegex.IsMatch(uid), SH.WebHoyolabInvalidUid);

        // 美服 UTC-05
        // 欧服 UTC+01
        // 其他 UTC+08
        return uid.AsSpan()[^9] switch
        {
            '6' => ServerRegionTimeZone.AmericaServerOffset,
            '7' => ServerRegionTimeZone.EuropeServerOffset,
            _ => ServerRegionTimeZone.CommonOffset,
        };
    }

    public static TimeSpan GetRegionTimeZoneUtcOffsetForRegion(in Region region)
    {
        // 美服 UTC-05
        // 欧服 UTC+01
        // 其他 UTC+08
        return region.Value switch
        {
            "os_usa" => ServerRegionTimeZone.AmericaServerOffset,
            "os_euro" => ServerRegionTimeZone.EuropeServerOffset,
            _ => ServerRegionTimeZone.CommonOffset,
        };
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value;
    }
}