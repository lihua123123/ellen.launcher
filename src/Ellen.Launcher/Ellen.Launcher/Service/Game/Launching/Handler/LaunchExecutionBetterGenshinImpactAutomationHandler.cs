// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Notification;
using Windows.System;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public sealed class LaunchExecutionBetterGenshinImpactAutomationHandler : AbstractLaunchExecutionHandler
{
    public override async ValueTask ExecuteAsync(LaunchExecutionContext context)
    {
        if (context.Process.IsRunning && context.LaunchOptions.UsingBetterGenshinImpactAutomation.Value)
        {
            await LaunchBetterGenshinImpactAsync(context).ConfigureAwait(false);
        }
    }

    private static async ValueTask LaunchBetterGenshinImpactAsync(LaunchExecutionContext context)
    {
        string? customPath = context.LaunchOptions.BetterGenshinImpactPath.Value;
        if (!string.IsNullOrEmpty(customPath))
        {
            if (!System.IO.File.Exists(customPath))
            {
                context.Messenger.Send(InfoBarMessage.Warning(SH.ServiceGameLaunchExecutionBetterGenshinImpactUrlProtocolNotRegistered));
                return;
            }

            try
            {
                SpinWaitPolyfill.SpinUntil(context.Process, static process => process.MainWindowHandle.Value is not 0);
            }
            catch (Exception ex)
            {
                context.Messenger.Send(InfoBarMessage.Error(SH.ServiceGameLaunchExecutionBetterGenshinImpactWaitGameMainWindowException, ex));
                return;
            }

            context.Messenger.Send(InfoBarMessage.Information(SH.ServiceGameLaunchExecutionBetterGenshinImpactStarted));
            System.Diagnostics.Process.Start(customPath);
            return;
        }

        Uri betterGenshinImpactUri = "bettergi://start".ToUri();
        if (await global::Windows.System.Launcher.QueryUriSupportAsync(betterGenshinImpactUri, LaunchQuerySupportType.Uri) is not LaunchQuerySupportStatus.Available)
        {
            context.Messenger.Send(InfoBarMessage.Warning(SH.ServiceGameLaunchExecutionBetterGenshinImpactUrlProtocolNotRegistered));
            return;
        }

        try
        {
            SpinWaitPolyfill.SpinUntil(context.Process, static process => process.MainWindowHandle.Value is not 0);
        }
        catch (Exception ex)
        {
            context.Messenger.Send(InfoBarMessage.Error(SH.ServiceGameLaunchExecutionBetterGenshinImpactWaitGameMainWindowException, ex));
            return;
        }

        context.Messenger.Send(InfoBarMessage.Information(SH.ServiceGameLaunchExecutionBetterGenshinImpactStarted));
        await global::Windows.System.Launcher.LaunchUriAsync(betterGenshinImpactUri);
    }
}