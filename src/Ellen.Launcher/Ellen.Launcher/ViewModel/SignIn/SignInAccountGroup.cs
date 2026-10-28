// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Ellen.Launcher.ViewModel.SignIn;

/// <summary>
/// 首页签到卡片中「一个账号」的分组：一行显示账号名 + 该账号下各游戏本次签到获得的物品图标，
/// 每个游戏具体是否签到不再单列，改由悬浮提示展开。
/// </summary>
public sealed partial class SignInAccountGroup : ObservableObject
{
    private readonly IReadOnlyList<SignInEntry> entries;

    public SignInAccountGroup(string accountName, IReadOnlyList<SignInEntry> entries)
    {
        AccountName = accountName;
        this.entries = entries;

        foreach (SignInEntry entry in entries)
        {
            entry.PropertyChanged += OnEntryPropertyChanged;
        }
    }

    public string AccountName { get; }

    /// <summary>
    /// 悬浮提示里的逐游戏明细。
    /// </summary>
    public IReadOnlyList<SignInEntry> Games => entries;

    /// <summary>
    /// 本次签到获得的物品（有图标的那些），一款游戏一件。
    /// </summary>
    public IReadOnlyList<SignInAward> Awards
    {
        get => [.. entries.Select(static entry => entry.Award).OfType<SignInAward>().Where(static award => award.HasIcon)];
    }

    public bool HasAwards
    {
        get => Awards.Count > 0;
    }

    public int SignedCount
    {
        get => entries.Count(static entry => entry.IsSign);
    }

    public int TotalCount
    {
        get => entries.Count;
    }

    /// <summary>
    /// 「已完成/总数」，例如 3/3。
    /// </summary>
    public string ProgressText
    {
        get => $"{SignedCount.ToString(System.Globalization.CultureInfo.CurrentCulture)}/{TotalCount.ToString(System.Globalization.CultureInfo.CurrentCulture)}";
    }

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 单个游戏签到状态或奖励变化后，刷新本账号的进度与物品列表
        if (e.PropertyName is nameof(SignInEntry.IsSign) or nameof(SignInEntry.Award))
        {
            OnPropertyChanged(nameof(SignedCount));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(Awards));
            OnPropertyChanged(nameof(HasAwards));
        }
    }
}
