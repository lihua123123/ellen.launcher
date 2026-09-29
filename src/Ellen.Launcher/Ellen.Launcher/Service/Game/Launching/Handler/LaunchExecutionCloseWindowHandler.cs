// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.LifeCycle;
using Ellen.Launcher.Core.Setting;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.UI.Windowing;
using Ellen.Launcher.UI.Xaml.View.Window;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public sealed class LaunchExecutionCloseWindowHandler : AbstractLaunchExecutionHandler
{
    public override async ValueTask ExecuteAsync(LaunchExecutionContext context)
    {
        if (LocalSetting.Get(SettingKeys.LaunchIsCloseWhenGameStarted, false))
        {
            XamlApplicationLifetime.WindowClosedForGameLaunch = true;

            await context.TaskContext.SwitchToMainThreadAsync();
            MainWindow.Instance.Close();
        }
    }

    public override async ValueTask AfterAsync(AfterLaunchExecutionContext context)
    {
        if (XamlApplicationLifetime.WindowClosedForGameLaunch)
        {
            XamlApplicationLifetime.WindowClosedForGameLaunch = false;

            ICurrentXamlWindowReference currentXamlWindowReference = context.ServiceProvider.GetRequiredService<ICurrentXamlWindowReference>();
            await context.TaskContext.SwitchToMainThreadAsync();

            // If the user has already manually reopened a window (e.g. via tray icon
            // or by launching a new instance), don't create a duplicate.
            if (currentXamlWindowReference.Window is not null)
            {
                return;
            }

            // Game has exited, recreate and show the main window
            MainWindow mainWindow = context.ServiceProvider.GetRequiredService<MainWindow>();
            currentXamlWindowReference.Window = mainWindow;
            mainWindow.SwitchTo();
            mainWindow.AppWindow.MoveInZOrderAtTop();
        }
    }
}
