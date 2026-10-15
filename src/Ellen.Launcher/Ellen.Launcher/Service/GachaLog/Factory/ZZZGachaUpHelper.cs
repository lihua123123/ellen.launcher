// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Generic;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

namespace Ellen.Launcher.Service.GachaLog.Factory;

/// <summary>
/// 绝区零调频记录 UP / 保底UP 判定。
/// 绝区零与原神 / 星铁为同一套保底引擎，机制如下（参考 Starward 抽卡统计实现）：
///  - 独家 / 独家重映频段（gacha_type 2 / 102）：首个 S 级 50% 为当期 UP 角色，50% 为标准 S 级角色（50/50 输）；输后下一个 S 级必为 UP。
///  - 音擎 / 音擎回响频段（gacha_type 3 / 103）：S 级 75% 为 UP 音擎，25% 为标准 S 级音擎；输后下一个 S 级必为 UP。
///  - 邦布频段（gacha_type 5）：S 级必定为当期 UP 邦布（无 50/50、无保底机制）。
///  - 常驻频段（gacha_type 1）：无 UP。
/// 因此：在限定 / 重映频段中，S 级物品当且仅当不是常驻（标准）物品时才算 UP。
/// 常驻物品集合 = 内置常驻 S 级物品表 ∪ 本存档在常驻频段（gacha_type 1）抽到的 S 级物品。
/// 常驻频段只出常驻物品，故后者可作为运行时兜底，补全未在表中列出的常驻音擎。
/// 保底UP（IsGuarantee）由 GachaStatisticsExtension.CompleteAdding 依据 IsUp 序列自动推导，本类不处理。
///
    /// 定制调频（3.1「漫长的告别」，国服 2026-07-29 08:00 上线）：柳 / 凯撒 / 朱鸢 三位限定 S 级代理人
    /// 及其专属音擎加入限定频段的「可歪」名单。2026-07-29 08:00 之后出现在限定卡池中的这三位及其音擎，
    /// 一律视同常驻（非 UP），表现即为去除 UP 标签、且紧邻的下一个 S 级由上游推导为保底UP。
    /// 该日期之前她们仍作为当期 UP 角色出现，故以物品时间为界做区分。
/// </summary>
public static class ZZZGachaUpHelper
{
    // 绝区零常驻 S 级角色（Star-Studded Cast / Stable Channel），item_id 为全局固定值。
    private static readonly HashSet<uint> StandardAgents = new()
    {
        1021u, // 猫又 Nekomata
        1041u, // 「11号」Soldier 11
        1101u, // 珂蕾妲 Koleda
        1141u, // 莱卡恩 Von Lycaon
        1181u, // 格莉丝 Grace
        1211u, // 丽娜 Rina
    };

    // 绝区零常驻 S 级音擎，item_id 为全局固定值。
    // 未在常驻频段抽到、又不在下表内的常驻音擎，会由 GetStandardItemIds 的运行时兜底补入。
    private static readonly HashSet<uint> StandardWEngines = new()
    {
        14104u, // 硫磺石（常驻 S 级音擎）
        14110u, // 燃狱齿轮 Hellfire Gears
        14118u, // 嵌合编译器 Fusion Compiler
        14121u, // 啜泣摇篮 Weeping Cradle
    };

    // 定制调频（3.1 版本「漫长的告别」，国服 2026-07-29 10:00 上线）起，以下三位限定 S 级代理人
    // 及其专属音擎加入限定频段的「可歪」名单：在任意限定卡池（独家 / 独家重映 / 音擎 / 音擎回响）歪到他们时，
    // 视同常驻（非 UP），紧邻的下一个 S 级由 CompleteAdding 推导为保底UP。
    // item_id 取自官方上线后的实际调频记录（与常驻名册区分：常驻为 1021/1041/1101/1141/1181/1211）。
    private static readonly DateTime CustomChannelStartTime = new(2026, 7, 29, 8, 0, 0);

    private static readonly HashSet<uint> CustomChannelAgents = new()
    {
        1071u, // 凯撒 Caesar
        1221u, // 柳 Yanagi
        1241u, // 朱鸢 Zhu Yuan
    };

    // 定制调频可歪专属音擎按名称匹配（音擎 item_id 不随版本稳定公开，且 ZZZ 记录 Name 可靠落库）。
    // 用名称前缀而非完整名，规避「防暴者Ⅵ型」等罗马数字字体变体。
    private static readonly HashSet<string> CustomChannelEngineNames = new()
    {
        "时流贤者", // 柳 专属音擎 Timeweaver
        "奔袭獠牙", // 凯撒 专属音擎
        "防暴者",   // 朱鸢 专属音擎 防暴者Ⅵ型（Riot Suppressor Mark VI）
    };

    /// <summary>
    /// 该物品是否属于定制调频可歪名单（柳 / 凯撒 / 朱鸢 三位代理人或其专属音擎）。
    /// 代理人按 item_id 精确匹配；音擎按名称前缀匹配。
    /// </summary>
    private static bool IsCustomChannelItem(GachaItem item)
    {
        if (CustomChannelAgents.Contains(item.ItemId))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(item.Name))
        {
            foreach (string engine in CustomChannelEngineNames)
            {
                if (item.Name.Contains(engine, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static HashSet<uint> GetStandardItemIds(IEnumerable<GachaItem> items)
    {
        HashSet<uint> standard = new(StandardAgents);
        foreach (uint id in StandardWEngines)
        {
            standard.Add(id);
        }

        // 运行时兜底：本存档在常驻频段（gacha_type 1）抽到的 S 级物品必为常驻物品。
        foreach (GachaItem item in items)
        {
            if (item.GachaType is GachaType.ZZZStandardChannel
                && (QualityType)item.Quality == QualityType.QUALITY_ORANGE
                && item.ItemId != 0u)
            {
                standard.Add(item.ItemId);
            }
        }

        return standard;
    }

    /// <summary>
    /// 判定某条绝区零调频记录是否为当期 UP（限定）物品。仅 S 级可能 UP，其余一律 false。
    /// </summary>
    public static bool IsUp(GachaItem item, HashSet<uint> standardItemIds)
    {
        if ((QualityType)item.Quality != QualityType.QUALITY_ORANGE)
        {
            return false;
        }

        // 定制调频：3.1 起柳 / 凯撒 / 朱鸢 及其专属音擎在限定池作为可歪角色，视同常驻（非 UP）。
        // 时间以国服上线时刻（2026-07-29 08:00）的墙钟时间为准；ZZZ 记录的 Time 墙钟即国服时间，
        // 实抓（偏移 +8）与导入（偏移 +0）两条路径都保留了该墙钟，故用 .DateTime 比较以屏蔽时区差异。
        // 返回 false 后，紧邻的下一个 S 级会由 CompleteAdding 自动标记为保底UP。
        if (item.Time.DateTime >= CustomChannelStartTime && IsCustomChannelItem(item))
        {
            return false;
        }

        return item.GachaType switch
        {
            GachaType.ZZZBangbooChannel => true,   // 邦布频段 S 级必定 UP
            GachaType.ZZZStandardChannel => false, // 常驻无 UP
            _ => !standardItemIds.Contains(item.ItemId), // 独家 / 重映 / 音擎 / 音擎回响：非标准 S 级即 UP
        };
    }
}
