// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.UI.Xaml.Controls;
using Ellen.Launcher.Factory.ContentDialog;

namespace Ellen.Launcher.Service.Game.Island;

/// <summary>
/// Embedded Yae 注入功能的开关守卫。
/// 「启动游戏」页面已被移除，注入开关不再有可操作的入口，因此把风险确认
/// 移到实际使用注入功能的地方（背包同步 / 成就导入 / 养成计划背包同步）。
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IEmbeddedYaeGate))]
public sealed partial class EmbeddedYaeGate : IEmbeddedYaeGate
{
    private readonly LaunchOptions launchOptions;
    private readonly IContentDialogFactory contentDialogFactory;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial EmbeddedYaeGate(IServiceProvider serviceProvider);

    public async ValueTask<bool> EnsureConfirmedAsync()
    {
        if (launchOptions.IsIslandEnabled.Value)
        {
            return true;
        }

        ContentDialogResult result = await contentDialogFactory
            .CreateForConfirmCancelAsync(
                SH.ViewModelEmbeddedYaeRiskConfirmTitle,
                SH.ViewModelEmbeddedYaeRiskConfirmContent,
                ContentDialogButton.Primary)
            .ConfigureAwait(false);

        if (result is not ContentDialogResult.Primary)
        {
            return false;
        }

        await taskContext.SwitchToMainThreadAsync();
        launchOptions.IsIslandEnabled.Value = true;
        return true;
    }
}
