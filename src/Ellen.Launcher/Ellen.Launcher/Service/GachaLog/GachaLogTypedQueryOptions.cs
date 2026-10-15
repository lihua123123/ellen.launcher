// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.GachaLog.QueryProvider;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Specialized;
using System.Web;

namespace Ellen.Launcher.Service.GachaLog;

public sealed class GachaLogTypedQueryOptions
{
    public const int Size = 20;
    public const int BeyondSize = 5;

    private readonly NameValueCollection innerQuery;

    public GachaLogTypedQueryOptions(in GachaLogQuery query, GachaType queryType)
    {
        IsOversea = query.IsOversea;
        Type = queryType;

        // HttpUtility.ParseQueryString 不会自动去掉 '?' 前面的 URL 部分，
        // 直接传完整 URL 会把 host+path+?+第一个参数名 合并成一个垃圾 key，
        // 导致 authkey_ver 等紧挨 '?' 的参数丢失 → 接口报 "auth key or sign type empty"。
        string rawQuery = query.Query;
        int questionIndex = rawQuery.IndexOf('?');
        innerQuery = HttpUtility.ParseQueryString(questionIndex >= 0 ? rawQuery[(questionIndex + 1)..] : rawQuery);

        if (query.Game is GachaGameType.ZZZ)
        {
            // 绝区零用 real_gacha_type 选卡池（不是 gacha_type）。
            // 参考 HoYo.Gacha: Game::Nap => ("real_gacha_type", "init_log_gacha_base_type")，
            // 以及 zzz-signal-search-export: request(`${url}&real_gacha_type=${key}`)。
            // 实测游戏请求里 gacha_type=2002 & real_gacha_type=2，即 gacha_type = 2000 + real_gacha_type。
            innerQuery.Set("real_gacha_type", $"{queryType:D}");
            innerQuery.Set("gacha_type", $"{2000 + (int)queryType}");
            // page 由 ToQueryString 按页递增设置，不在此删除
        }
        else
        {
            innerQuery.Set("gacha_type", $"{queryType:D}");
        }

        if (query.Game is GachaGameType.ZZZ && string.IsNullOrEmpty(innerQuery.Get("game_biz")))
        {
            // 绝区零调频接口必须携带 game_biz，缺失时服务端返回 -111 game name error；
            // 链接中自带时保留原值。
            innerQuery.Set("game_biz", IsOversea ? "nap_global" : "nap_cn");
        }

        if (queryType.ToString().StartsWith("UGC"))
        {
            innerQuery.Set("size", $"{BeyondSize}");
        }
        else
        {
            innerQuery.Set("size", $"{Size}");
        }
    }

    public bool IsOversea { get; }

    public GachaType Type { get; }

    public long EndId { get; set; }

    /// <summary>
    /// 分页页码，绝区零用 page+end_id 双管齐下分页（与 zzz-signal-search-export 一致）
    /// </summary>
    public int Page { get; set; } = 1;

    public string ToQueryString()
    {
        // Make the cached end id into query.
        innerQuery.Set("end_id", $"{EndId:D}");

        // 绝区零分页：page 递增（游戏 API 每页返回 5 条，用 page 翻页）
        if (Type.ToString().StartsWith("ZZZ"))
        {
            innerQuery.Set("page", $"{Page:D}");
        }

        string? query = innerQuery.ToString();
        ArgumentException.ThrowIfNullOrEmpty(query);
        return query;
    }
}