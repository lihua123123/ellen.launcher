// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Ellen.Launcher.ViewModel.Abstraction;
using Ellen.Launcher.ViewModel.Home;

namespace Ellen.Launcher.UI.Xaml.View.Card;

public sealed partial class LaunchGameCard : UserControl
{
    public LaunchGameCard(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        this.InitializeViewModelSlim<LaunchGameCardViewModel>(serviceProvider);
        PointerEntered += OnPointerEntered;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 鼠标移入时静默刷新一次账号与运行状态（游戏可能刚退出，账号可能刚切换）。
    /// </summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (DataContext is LaunchGameCardViewModel viewModel && viewModel.RefreshCommand.CanExecute(null))
        {
            viewModel.RefreshCommand.Execute(null);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        (DataContext as LaunchGameCardViewModel)?.StopRunningStatePolling();
    }
}
