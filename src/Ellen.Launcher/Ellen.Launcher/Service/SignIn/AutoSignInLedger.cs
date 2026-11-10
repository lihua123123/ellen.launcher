// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using System.Globalization;

namespace Ellen.Launcher.Service.SignIn;

/// <summary>
/// 「今天哪些账号、哪些角色已经确认签过」的账本，持久化在本地设置里。
/// <para>
/// 以日期为界：日期一变整本作废，等价于「冷却到当天 23:59:59」后自动重来。
/// 粒度到「账号 + 角色」，所以新增账号或新增角色时只会补签缺的那部分，
/// 已经签过的角色不会再发任何请求。
/// </para>
/// </summary>
internal sealed class AutoSignInLedger
{
    private const string VersionTag = "v1";
    private const string DateFormat = "yyyy-MM-dd";
    private const char FieldSeparator = '|';
    private const char ItemSeparator = ',';

    private readonly HashSet<string> handledRoles;
    private readonly HashSet<Guid> completedAccounts;
    private bool dirty;

    private AutoSignInLedger(DateOnly date, HashSet<string> handledRoles, HashSet<Guid> completedAccounts, bool dirty)
    {
        Date = date;
        this.handledRoles = handledRoles;
        this.completedAccounts = completedAccounts;
        this.dirty = dirty;
    }

    /// <summary>
    /// 账本所属的日期（本机自然日）。
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// 读取今日账本；跨天、版本不符或内容损坏时返回一本空账本。
    /// </summary>
    public static AutoSignInLedger LoadOrCreate(DateOnly today)
    {
        string raw = LocalSetting.Get(SettingKeys.SignInAutoSignInLedger, string.Empty);
        if (string.IsNullOrEmpty(raw))
        {
            return new(today, [], [], false);
        }

        string[] fields = raw.Split(FieldSeparator);
        if (fields.Length is not 4 || fields[0] != VersionTag)
        {
            return new(today, [], [], true);
        }

        if (!DateOnly.TryParseExact(fields[1], DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            || date != today)
        {
            return new(today, [], [], true);
        }

        HashSet<string> roles = new(fields[2].Split(ItemSeparator, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

        HashSet<Guid> accounts = [];
        foreach (string item in fields[3].Split(ItemSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(item, out Guid accountId))
            {
                accounts.Add(accountId);
            }
        }

        return new(today, roles, accounts, false);
    }

    public static string GetRoleKey(SignInGameRole role)
    {
        return $"{role.Game}:{role.Uid}";
    }

    public bool IsRoleHandled(SignInGameRole role)
    {
        return handledRoles.Contains(GetRoleKey(role));
    }

    public void MarkRoleHandled(SignInGameRole role)
    {
        dirty |= handledRoles.Add(GetRoleKey(role));
    }

    public bool IsAccountCompleted(Guid accountId)
    {
        return completedAccounts.Contains(accountId);
    }

    public void MarkAccountCompleted(Guid accountId)
    {
        dirty |= completedAccounts.Add(accountId);
    }

    public void MarkAccountIncomplete(Guid accountId)
    {
        dirty |= completedAccounts.Remove(accountId);
    }

    /// <summary>
    /// 写回本地设置。没有变化时直接返回，避免每次启动都重写设置文件。
    /// </summary>
    /// <returns>是否真的写入了内容。</returns>
    public bool Save()
    {
        if (!dirty)
        {
            return false;
        }

        string roles = string.Join(ItemSeparator, handledRoles.Order(StringComparer.Ordinal));
        string accounts = string.Join(ItemSeparator, completedAccounts.Order().Select(static id => id.ToString("D")));
        string value = string.Join(
            FieldSeparator,
            VersionTag,
            Date.ToString(DateFormat, CultureInfo.InvariantCulture),
            roles,
            accounts);

        LocalSetting.Set(SettingKeys.SignInAutoSignInLedger, value);
        dirty = false;
        return true;
    }
}
