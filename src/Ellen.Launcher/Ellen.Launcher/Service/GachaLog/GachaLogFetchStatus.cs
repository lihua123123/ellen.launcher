// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Globalization;

namespace Ellen.Launcher.Service.GachaLog;

public sealed class GachaLogFetchStatus
{
    public GachaLogFetchStatus(GachaType configType)
    {
        ConfigType = configType;
    }

    public bool AuthKeyTimeout { get; set; }

    public GachaType ConfigType { get; }

    public List<Item> Items { get; } = new(GachaLogTypedQueryOptions.Size);

    public string Header
    {
        get
        {
            return AuthKeyTimeout
                ? SH.ViewDialogGachaLogRefreshProgressAuthkeyTimeout
                : SH.FormatViewDialogGachaLogRefreshProgressDescription(ConfigType.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture));
        }
    }
}