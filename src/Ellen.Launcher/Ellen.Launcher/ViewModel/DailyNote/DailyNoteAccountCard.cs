// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Model.Entity;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Ellen.Launcher.ViewModel.DailyNote;

/// <summary>
/// 一个米游社账号对应的一张便笺卡片。
/// 顶部是账号头像 + 昵称 + UID 与整张卡共用的操作按钮，
/// 卡内每个角色（原神 / 绝区零）横向并排一列，整张卡左下角只有一条刷新时间。
///
/// 目前只承载原神与绝区零两种便笺，不包含星穹铁道。
/// </summary>
public sealed partial class DailyNoteAccountCard : ObservableObject
{
    /// <summary>
    /// 一张卡最多并排几列（页面上是固定的几个 ContentControl 槽位）。
    /// </summary>
    public const int MaxColumns = 4;

    /// <summary>
    /// 单列宽度。
    /// </summary>
    public const double ColumnWidth = 308D;

    /// <summary>
    /// 列与列之间分隔线占用的宽度（1px 线 + 左右各 12 的间距）。
    /// </summary>
    public const double ColumnSeparatorWidth = 25D;

    /// <summary>
    /// 卡片左右内边距之和（Border Padding=8）。
    /// </summary>
    public const double CardHorizontalPadding = 16D;

    private DailyNoteAccountCard(Guid userId, IReadOnlyList<object> columns, IReadOnlyList<DailyNoteEntry> genshinEntries)
    {
        UserId = userId;
        Columns = columns;
        GenshinEntries = genshinEntries;

        foreach (object column in columns)
        {
            switch (column)
            {
                case DailyNoteEntry entry:
                    entry.PropertyChanged += OnColumnPropertyChanged;
                    break;
                case ZenlessNoteEntry entry:
                    entry.PropertyChanged += OnColumnPropertyChanged;
                    break;
            }
        }
    }

    public Guid UserId { get; }

    /// <summary>
    /// 卡内的列：原神角色在前，绝区零角色在后。
    /// </summary>
    public IReadOnlyList<object> Columns { get; }

    /// <summary>
    /// 卡内的原神便笺；只有这些能删除 / 配置通知（绝区零便笺是派生数据，不落库）。
    /// </summary>
    public IReadOnlyList<DailyNoteEntry> GenshinEntries { get; }

    /// <summary>
    /// 账号名：取不到昵称时回退为「米游社账号」。
    /// </summary>
    public string AccountDisplayName { get; private init; } = SH.ViewPageDailyNoteAccountGroupNoId;

    public Uri? AccountAvatarUri { get; private init; }

    public string? AccountAid { get; private init; }

    public bool HasAccountUid
    {
        get => !string.IsNullOrEmpty(AccountAid);
    }

    public string AccountUidText
    {
        get => AccountAid ?? string.Empty;
    }

    /// <summary>
    /// 卡片宽度跟随列数：一列单宽，两列两倍宽，以此类推。
    /// </summary>
    public double CardWidth
    {
        get
        {
            int count = Math.Max(Columns.Count, 1);
            return (count * ColumnWidth) + ((count - 1) * ColumnSeparatorWidth) + CardHorizontalPadding;
        }
    }

    /// <summary>
    /// 固定槽位：页面不嵌套列表（嵌套 ItemsControl 在自定义面板里测不准高度），
    /// 直接用若干 ContentControl 依次排；一个账号下最多 <see cref="MaxColumns"/> 列。
    /// </summary>
    public object? Column0
    {
        get => Columns.Count > 0 ? Columns[0] : null;
    }

    public object? Column1
    {
        get => Columns.Count > 1 ? Columns[1] : null;
    }

    public object? Column2
    {
        get => Columns.Count > 2 ? Columns[2] : null;
    }

    public object? Column3
    {
        get => Columns.Count > 3 ? Columns[3] : null;
    }

    public bool HasColumn1
    {
        get => Columns.Count > 1;
    }

    public bool HasColumn2
    {
        get => Columns.Count > 2;
    }

    public bool HasColumn3
    {
        get => Columns.Count > 3;
    }

    /// <summary>
    /// 通知设置菜单里的原神角色槽位。
    /// </summary>
    public DailyNoteEntry? GenshinEntry0
    {
        get => GenshinEntries.Count > 0 ? GenshinEntries[0] : null;
    }

    public DailyNoteEntry? GenshinEntry1
    {
        get => GenshinEntries.Count > 1 ? GenshinEntries[1] : null;
    }

    public DailyNoteEntry? GenshinEntry2
    {
        get => GenshinEntries.Count > 2 ? GenshinEntries[2] : null;
    }

    public DailyNoteEntry? GenshinEntry3
    {
        get => GenshinEntries.Count > 3 ? GenshinEntries[3] : null;
    }

    public bool HasGenshinEntry1
    {
        get => GenshinEntries.Count > 1;
    }

    public bool HasGenshinEntry2
    {
        get => GenshinEntries.Count > 2;
    }

    public bool HasGenshinEntry3
    {
        get => GenshinEntries.Count > 3;
    }

    public bool HasGenshinEntries
    {
        get => GenshinEntries.Count > 0;
    }

    /// <summary>
    /// 整张卡共用的一条刷新时间（取卡内各列最近的一次）。
    /// </summary>
    public string RefreshTimeFormatted
    {
        get
        {
            DateTimeOffset latest = default;
            foreach (object column in Columns)
            {
                DateTimeOffset time = column switch
                {
                    DailyNoteEntry entry => entry.RefreshTime,
                    ZenlessNoteEntry entry => entry.RefreshTime,
                    _ => default,
                };

                if (time > latest)
                {
                    latest = time;
                }
            }

            return latest == default
                ? SH.ModelEntityDailyNoteNotRefreshed
                : SH.FormatModelEntityDailyNoteRefreshTime(latest.ToLocalTime());
        }
    }

    /// <summary>
    /// 按米游社账号把原神便笺与绝区零便笺合成一张张卡片。
    /// </summary>
    /// <param name="genshinEntries">原神便笺。</param>
    /// <param name="zenlessEntries">绝区零便笺。</param>
    /// <param name="accountLookup">按账号 Id 取昵称 / 头像 / Aid。</param>
    public static ObservableCollection<DailyNoteAccountCard> CreateFrom(
        IEnumerable<DailyNoteEntry> genshinEntries,
        IEnumerable<ZenlessNoteEntry> zenlessEntries,
        Func<Guid, (string? Nickname, Uri? AvatarUri, string? Aid)> accountLookup)
    {
        List<DailyNoteAccountCard> cards = [];
        List<Guid> order = [];
        Dictionary<Guid, List<object>> columnsByUser = [];
        Dictionary<Guid, List<DailyNoteEntry>> genshinByUser = [];

        List<object> GetColumns(Guid userId)
        {
            if (!columnsByUser.TryGetValue(userId, out List<object>? columns))
            {
                columns = [];
                columnsByUser[userId] = columns;
                genshinByUser[userId] = [];
                order.Add(userId);
            }

            return columns;
        }

        foreach (DailyNoteEntry entry in genshinEntries)
        {
            GetColumns(entry.UserId).Add(entry);
            genshinByUser[entry.UserId].Add(entry);
        }

        foreach (ZenlessNoteEntry entry in zenlessEntries)
        {
            GetColumns(entry.UserId).Add(entry);
        }

        foreach (Guid userId in order)
        {
            List<object> columns = columnsByUser[userId];
            if (columns.Count > MaxColumns)
            {
                columns = columns.GetRange(0, MaxColumns);
            }

            (string? nickname, Uri? avatarUri, string? aid) = accountLookup(userId);

            cards.Add(new(userId, columns, genshinByUser[userId])
            {
                AccountDisplayName = string.IsNullOrEmpty(nickname) ? SH.ViewPageDailyNoteAccountGroupNoId : nickname!,
                AccountAvatarUri = avatarUri,
                AccountAid = aid ?? (columns.Count > 0 && columns[0] is DailyNoteEntry first ? first.User?.Aid : null),
            });
        }

        return [.. cards];
    }

    private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RefreshTimeFormatted))
        {
            OnPropertyChanged(nameof(RefreshTimeFormatted));
        }
    }
}
