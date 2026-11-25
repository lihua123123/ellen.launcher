// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Control;
using Ellen.Launcher.ViewModel.Cultivation;

namespace Ellen.Launcher.UI.Xaml.View.Page;

public sealed partial class CultivationPage : ScopedPage
{
    public CultivationPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 命中 Frame 缓存（`CultivationPage.xaml` 根节点上的 `NavigationCacheMode="Required"`）：
    /// 离开视觉树时不拆 ViewModel / DI 作用域 / CancellationToken，返回时已实现的卡片还在，
    /// 第二次进入就不用再付一遍实例化成本。与背包页同一套做法。
    /// </summary>
    protected override bool PreserveOnUnload => true;

    protected override void LoadingOverride()
    {
        InitializeDataContext<CultivationViewModel>();
    }
}
