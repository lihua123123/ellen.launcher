// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Core.LifeCycle.InterProcess.Yae;
using Ellen.Launcher.Service.Game.Island;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Yae.Achievement;
using Ellen.Launcher.Win32;
using Ellen.Launcher.Win32.Foundation;
using System.IO;

namespace Ellen.Launcher.Service.Game.Launching.Handler;

public sealed class LaunchExecutionYaeNamedPipeHandler : AbstractLaunchExecutionHandler
{
    private readonly TargetNativeConfiguration config;
    private readonly YaeDataArrayReceiver receiver;

    public LaunchExecutionYaeNamedPipeHandler(TargetNativeConfiguration config, YaeDataArrayReceiver receiver)
    {
        this.config = config;
        this.receiver = receiver;
    }

    public override async ValueTask ExecuteAsync(LaunchExecutionContext context)
    {
        if (!EllenRuntime.IsProcessElevated)
        {
            context.Process.Kill();
            EllenException.NotSupported(SH.ServiceGameLaunchingHandlerEmbeddedYaeClientNotElevated);
        }

        if (!context.LaunchOptions.IsIslandEnabled.Value)
        {
            context.Process.Kill();
            EllenException.NotSupported(SH.ServiceGameLaunchingHandlerEmbeddedYaeIslandNotEnabled);
            return;
        }

        string dataFolderYaePath = Path.Combine(EllenRuntime.DataDirectory, "YaeAchievementLib.dll");
        InstalledLocation.CopyFileFromApplicationUri("ms-appx:///YaeAchievementLib.dll", dataFolderYaePath);

        try
        {
            DllInjectionUtilities.InjectUsingRemoteThread(dataFolderYaePath, "YaeMain", context.Process.Id);
        }
        catch (Exception ex)
        {
            // Windows Defender Application Control
            if (EllenNative.IsWin32(ex.HResult, WIN32_ERROR.ERROR_SYSTEM_INTEGRITY_POLICY_VIOLATION))
            {
                context.Process.Kill();
                throw EllenException.Throw(SH.ServiceGameLaunchingHandlerEmbeddedYaeErrorSystemIntegrityPolicyViolation);
            }

            throw;
        }

        try
        {
#pragma warning disable CA2007
            await using (YaeNamedPipeServer server = new(context.ServiceProvider, context.Process, config))
#pragma warning restore CA2007
            {
                receiver.Array = await server.GetDataArrayAsync().ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            context.Process.Kill();
            throw;
        }
    }
}