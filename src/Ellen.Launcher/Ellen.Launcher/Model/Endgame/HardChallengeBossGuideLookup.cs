// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Endgame;

/// <summary>
/// 把挑战记录里的怪物名匹配到内置攻略里的 Boss。
/// <para>
/// 记录数据（HoYoLAB）里的怪物名通常是短名（如「浪涛锚定者」），
/// 而攻略里同时保留了短名与全名（如「浪涛锚定者·狂飙突噬」），因此需要多级匹配。
/// </para>
/// </summary>
public static class HardChallengeBossGuideLookup
{
    /// <summary>
    /// 在指定期数（优先）与全部期数里查找与怪物名对应的 Boss。
    /// </summary>
    /// <param name="periods">内置攻略期数集合。</param>
    /// <param name="monsterName">挑战记录里的怪物名。</param>
    /// <param name="preferredVersion">优先匹配的期数（一般为当前记录所在的期）。</param>
    public static HardChallengeBossGuideBoss? Find(
        ImmutableArray<HardChallengeBossGuidePeriod> periods,
        string? monsterName,
        string? preferredVersion = null)
    {
        if (periods.IsDefaultOrEmpty || string.IsNullOrWhiteSpace(monsterName))
        {
            return null;
        }

        // 1. 优先在记录所属期里匹配，避免跨期同名误配
        if (!string.IsNullOrEmpty(preferredVersion) &&
            periods.FirstOrDefault(p => string.Equals(p.Version, preferredVersion, StringComparison.Ordinal)) is { } preferred)
        {
            foreach (HardChallengeBossGuideBoss boss in preferred.Bosses)
            {
                if (IsMatch(boss, monsterName))
                {
                    return boss;
                }
            }
        }

        // 2. 其余期数从新到旧，先精确后模糊
        for (int i = periods.Length - 1; i >= 0; i--)
        {
            foreach (HardChallengeBossGuideBoss boss in periods[i].Bosses)
            {
                if (IsMatch(boss, monsterName))
                {
                    return boss;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 按「第 N 期」的序号找到对应版本的攻略。
    /// <para>
    /// 内置攻略的期数与游戏内的期数一一对应且都按时间升序（第 1 期 = 攻略里最早的一期），
    /// 因此序号直接作为下标；攻略比期数旧、或序号越界时退回最新一期。
    /// </para>
    /// </summary>
    /// <param name="periods">内置攻略期数（升序）。</param>
    /// <param name="scheduleNumber">期数序号（1 开始）。</param>
    public static HardChallengeBossGuidePeriod? FindPeriodByNumber(ImmutableArray<HardChallengeBossGuidePeriod> periods, int scheduleNumber)
    {
        if (periods.IsDefaultOrEmpty)
        {
            return null;
        }

        int index = scheduleNumber - 1;
        if (index < 0 || index >= periods.Length)
        {
            index = periods.Length - 1;
        }

        return periods[index];
    }

    private static bool IsMatch(HardChallengeBossGuideBoss boss, string name)
    {
        if (boss.ShortName.Length > 0)
        {
            if (string.Equals(boss.ShortName, name, StringComparison.Ordinal))
            {
                return true;
            }

            // 「浪涛锚定者」 vs 「浪涛锚定者·狂飙突噬」
            if (name.StartsWith(boss.ShortName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (boss.FullName.Length > 0)
        {
            if (string.Equals(boss.FullName, name, StringComparison.Ordinal))
            {
                return true;
            }

            if (boss.FullName.StartsWith(name, StringComparison.Ordinal) ||
                boss.FullName.Contains(name, StringComparison.Ordinal) ||
                name.Contains(boss.FullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
