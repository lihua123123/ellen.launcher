// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Web.Hoyolab;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Ellen.Launcher.Service;

public static class KnownServerRegionTimeZones
{
    public static ImmutableArray<NameValue<TimeSpan>> Value
    {
        get
        {
            Debug.Assert(XamlApplicationLifetime.CultureInfoInitialized);
            return !field.IsDefault ? field : field =
            [
                new(SH.ServiceAppOptionsCalendarServerTimeZoneCommon, ServerRegionTimeZone.CommonOffset),
                new(SH.ServiceAppOptionsCalendarServerTimeZoneAmerica, ServerRegionTimeZone.AmericaServerOffset),
                new(SH.ServiceAppOptionsCalendarServerTimeZoneEurope, ServerRegionTimeZone.EuropeServerOffset),
            ];
        }
    }
}