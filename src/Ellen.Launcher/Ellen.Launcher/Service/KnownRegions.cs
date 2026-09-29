// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Web.Hoyolab;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Ellen.Launcher.Service;

public static class KnownRegions
{
    public static ImmutableArray<NameValue<Region>> Value
    {
        get
        {
            Debug.Assert(XamlApplicationLifetime.CultureInfoInitialized);
            return !field.IsDefault ? field : field =
            [
                new(SH.WebHoyolabRegionCNGF01, Region.CNGF01),
                new(SH.WebHoyolabRegionCNQD01, Region.CNQD01),
                new(SH.WebHoyolabRegionOSUSA, Region.OSUSA),
                new(SH.WebHoyolabRegionOSEURO, Region.OSEURO),
                new(SH.WebHoyolabRegionOSASIA, Region.OSASIA),
                new(SH.WebHoyolabRegionOSCHT, Region.OSCHT),
            ];
        }
    }
}