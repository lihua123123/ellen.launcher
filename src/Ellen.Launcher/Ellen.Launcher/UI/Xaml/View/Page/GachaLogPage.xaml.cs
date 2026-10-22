// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class GachaLogPage : ScopedPage
{
    private GachaLogViewModel? viewModel;

    public GachaLogPage()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        // Segmented 在 InitializeComponent 解析期间就会触发 SelectionChanged，
        // 那时后面的 x:Name 字段还没赋值，所以解析完成后再同步一次。
        UpdateStatisticsPanes();
    }

    protected override void LoadingOverride()
    {
        InitializeDataContext<GachaLogViewModel>();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (viewModel is not null)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        viewModel = args.NewValue as GachaLogViewModel;

        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateStatisticsPanes();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GachaLogViewModel.IsZZZMode))
        {
            UpdateStatisticsPanes();
        }
    }

    private void OnStatisticsSegmentedSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStatisticsPanes();
    }

    /// <summary>
    /// 原神按「历史 / 角色 / 武器」分页；绝区零没有可还原的卡池历史页签，
    /// 只保留「角色 / 音擎·邦布」两页，并复用同一套统计面板。
    /// </summary>
    private void UpdateStatisticsPanes()
    {
        if (StatisticsHistoryPane is null
            || StatisticsAvatarPane is null
            || StatisticsWeaponPane is null)
        {
            return;
        }

        bool isZZZ = viewModel?.IsZZZMode is true;
        UpdateQualityLabels(isZZZ);

        Segmented? segmented = isZZZ ? StatisticsSegmentedZZZ : StatisticsSegmentedGenshin;
        if (segmented is null || segmented.Items.Count == 0)
        {
            return;
        }

        int index = segmented.SelectedIndex;
        if (index < 0 || index >= segmented.Items.Count)
        {
            index = 0;
            segmented.SelectedIndex = 0;
        }

        if (isZZZ)
        {
            StatisticsHistoryPane.Visibility = index is 0 ? Visibility.Visible : Visibility.Collapsed;
            StatisticsAvatarPane.Visibility = index is 1 ? Visibility.Visible : Visibility.Collapsed;
            StatisticsWeaponPane.Visibility = index is 2 ? Visibility.Visible : Visibility.Collapsed;
            return;
        }

        StatisticsHistoryPane.Visibility = index is 0 ? Visibility.Visible : Visibility.Collapsed;
        StatisticsAvatarPane.Visibility = index is 1 ? Visibility.Visible : Visibility.Collapsed;
        StatisticsWeaponPane.Visibility = index is 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 原神用「五星 / 四星 / 三星」，绝区零用「S / A / B 级」。
    /// 直接设置 Text 而不是绑定，避免切换游戏时文案残留。
    /// </summary>
    private void UpdateQualityLabels(bool isZZZ)
    {
        if (AvatarOrangeLabel is null
            || AvatarPurpleLabel is null
            || WeaponOrangeLabel is null
            || WeaponPurpleLabel is null
            || WeaponBlueLabel is null
            || HistoryOrangeLabel is null
            || HistoryPurpleLabel is null
            || HistoryBlueLabel is null)
        {
            return;
        }

        AvatarOrangeLabel.Text = isZZZ ? SH.ViewControlStatisticsCardOrangeQualityZZZ : SH.ViewControlStatisticsCardOrangeText;
        AvatarPurpleLabel.Text = isZZZ ? SH.ViewControlStatisticsCardPurpleQualityZZZ : SH.ViewControlStatisticsCardPurpleText;
        WeaponOrangeLabel.Text = AvatarOrangeLabel.Text;
        WeaponPurpleLabel.Text = AvatarPurpleLabel.Text;
        WeaponBlueLabel.Text = isZZZ ? SH.ViewControlStatisticsCardBlueQualityZZZ : SH.ViewControlStatisticsCardBlueText;
        HistoryOrangeLabel.Text = AvatarOrangeLabel.Text;
        HistoryPurpleLabel.Text = AvatarPurpleLabel.Text;
        HistoryBlueLabel.Text = WeaponBlueLabel.Text;
    }
}