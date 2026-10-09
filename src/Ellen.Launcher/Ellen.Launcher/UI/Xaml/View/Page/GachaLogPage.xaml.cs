// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class GachaLogPage : ScopedPage
{
    public GachaLogPage()
    {
        InitializeComponent();

        // Segmented 在 InitializeComponent 解析期间就会触发 SelectionChanged，
        // 那时后面的 x:Name 字段还没赋值，所以解析完成后再同步一次。
        UpdateStatisticsPanes();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<GachaLogViewModel>();
    }

    /// <summary>
    /// 「统计」下的二级标签：0 = 历史，1 = 角色，2 = 武器。
    /// </summary>
    private void OnStatisticsSegmentedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatisticsPanes();
    }

    private void UpdateStatisticsPanes()
    {
        if (StatisticsSegmented is null
            || StatisticsHistoryPane is null
            || StatisticsAvatarPane is null
            || StatisticsWeaponPane is null)
        {
            return;
        }

        int index = StatisticsSegmented.SelectedIndex;
        if (index < 0)
        {
            index = 0;
            StatisticsSegmented.SelectedIndex = 0;
        }

        StatisticsHistoryPane.Visibility = index is 0 ? Visibility.Visible : Visibility.Collapsed;
        StatisticsAvatarPane.Visibility = index is 1 ? Visibility.Visible : Visibility.Collapsed;
        StatisticsWeaponPane.Visibility = index is 2 ? Visibility.Visible : Visibility.Collapsed;
    }
}