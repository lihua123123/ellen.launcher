// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Immutable;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

public sealed partial class UIGFUidSelection
{
    public UIGFUidSelection(GachaGameType game, ulong uid)
    {
        Game = game;
        Uid = uid;
    }

    public ulong Uid { get; }

    public GachaGameType Game { get; }

    public string GameName => Game is GachaGameType.ZZZ ? SH.ViewPageGachaLogGameZZZ : SH.ViewPageGachaLogGameGenshin;

    public static ImmutableArray<(GachaGameType Game, ulong Uid)> GetSelectedAccounts(ListViewBase listView)
    {
        return [.. listView.SelectedItems.Cast<UIGFUidSelection>().Select(static item => (item.Game, item.Uid))];
    }
}
