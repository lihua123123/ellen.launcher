// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Win32.Foundation;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public sealed class LaunchExecutionGameProcessStartHandler : AbstractLaunchExecutionHandler
{
    // The BeforeAsync override that used to sync the "Snap.ContentDelivery" repository has been
    // removed together with the Launch Game page. That component only provided the FullTrust
    // helper process used by the normal launch path (GameProcessFactory.CreateUsingFullTrustSuspended),
    // which no longer has any UI. Embedded Yae starts the game through a plain suspended process
    // (GameProcessFactory.CreateForEmbeddedYae) and does not need it - keeping the sync here only
    // made every Embedded Yae feature fail or download the metadata repository into the wrong folder.
    public override async ValueTask ExecuteAsync(LaunchExecutionContext context)
    {
        try
        {
            context.Process.Start();
            await context.TaskContext.SwitchToMainThreadAsync();
            GameLifeCycle.IsGameRunningProperty.Value = true;
        }
        catch (Win32Exception ex)
        {
            if (ex.HResult is HRESULT.E_FAIL)
            {
                return;
            }

            throw;
        }

        context.Progress.Report(new(SH.ServiceGameLaunchPhaseProcessStarted));
    }
}