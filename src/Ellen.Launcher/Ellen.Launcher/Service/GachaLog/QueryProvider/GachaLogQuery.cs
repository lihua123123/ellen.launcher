// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Service.GachaLog.QueryProvider;

public readonly struct GachaLogQuery
{
    public readonly string Message;
    public readonly string Query;
    public readonly bool IsOversea;
    public readonly GachaGameType Game;

    public GachaLogQuery(string query, GachaGameType game = GachaGameType.Genshin)
    {
        Query = query;
        Game = game;
        IsOversea = IsOverseaQuery(query);
        Message = string.Empty;
    }

    private GachaLogQuery(string query, string message)
    {
        Query = query;
        Message = message;
    }

    public bool IsInvalid { get => string.IsNullOrEmpty(Message); }

    public static GachaLogQuery Invalid(string message)
    {
        return new(string.Empty, message);
    }

    private static bool IsOverseaQuery(string query)
    {
        // 国际服域名
        if (query.Contains("-sg.hoyoverse.com", StringComparison.OrdinalIgnoreCase)
            || query.Contains("gs.hoyoverse.com", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 原神国际服区服
        if (query.Contains("region=os_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 绝区零国际服区服（国服为 prod_gf_cn）
        return query.Contains("region=prod_gf_", StringComparison.OrdinalIgnoreCase)
            && !query.Contains("region=prod_gf_cn", StringComparison.OrdinalIgnoreCase);
    }
}
