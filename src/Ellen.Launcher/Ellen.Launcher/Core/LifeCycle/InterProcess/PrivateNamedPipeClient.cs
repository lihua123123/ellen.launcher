// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Windows.AppLifecycle;
using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.LifeCycle.InterProcess.Model;
using Ellen.Launcher.Factory.Process;
using System.IO;
using System.IO.Pipes;

namespace Ellen.Launcher.Core.LifeCycle.InterProcess;

[Service(ServiceLifetime.Singleton)]
public sealed partial class PrivateNamedPipeClient : IDisposable
{
    private readonly NamedPipeClientStream clientStream = new(".", PrivateNamedPipe.PrivateName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

    public bool TryRedirectActivationTo(AppActivationArguments args)
    {
        return TryRedirectActivationTo(EllenActivationArguments.FromAppActivationArguments(args, isRedirected: true));
    }

    public bool TryRedirectActivationTo(EllenActivationArguments ellenArgs)
    {
        if (!clientStream.TryConnectOnce())
        {
            return false;
        }

        try
        {
            clientStream.WritePacket(PrivateNamedPipe.PrivateVersion, PipePacketType.Request, PipePacketCommand.RequestElevationStatus);
            clientStream.ReadPacket(out PipePacketHeader _, out ElevationStatusResponse? response);
            ArgumentNullException.ThrowIfNull(response);

            // Prefer elevated instance
            if (EllenRuntime.IsProcessElevated && !response.IsElevated)
            {
                // Notify previous instance to exit
                clientStream.WritePacket(PrivateNamedPipe.PrivateVersion, PipePacketType.SessionTermination, PipePacketCommand.Exit);
                clientStream.Flush();
                WaitPreviousProcessExit(response);

                // Retain the elevated instance
                return false;
            }

            // Redirect to previous instance
            clientStream.WritePacketWithJsonContent(PrivateNamedPipe.PrivateVersion, PipePacketType.Request, PipePacketCommand.RedirectActivation, ellenArgs);
            clientStream.WritePacket(PrivateNamedPipe.PrivateVersion, PipePacketType.SessionTermination, PipePacketCommand.None);
            clientStream.Flush();

            return true;
        }
        catch (IOException)
        {
            // Pipe is broken.
            return false;
        }
    }

    /// <summary>
    /// Lightweight one-shot redirect from the second instance to the first,
    /// without needing DI or the singleton pipe client. Called from <see cref="Bootstrap.Main"/>.
    /// </summary>
    /// <param name="args">The command-line arguments from Main().</param>
    /// <returns>true if a redirect was sent; false if pipe unavailable.</returns>
    public static bool TryLightweightRedirect(string[] args)
    {
        EllenActivationArguments ellenArgs;

        // Determine activation kind from command-line arguments
        if (args.Length > 0)
        {
            string? arg = args[0];
            if (arg is not null && arg.StartsWith("ellen://", StringComparison.OrdinalIgnoreCase))
            {
                ellenArgs = new()
                {
                    Kind = EllenActivationKind.Protocol,
                    ProtocolActivatedUri = new Uri(arg),
                    IsRedirectTo = true,
                };
            }
            else
            {
                // Regular launch with arguments
                ellenArgs = new()
                {
                    Kind = EllenActivationKind.Launch,
                    LaunchActivatedArguments = arg,
                    IsRedirectTo = true,
                };
            }
        }
        else
        {
            // No arguments, treat as a regular launch to bring existing instance to foreground
            ellenArgs = new()
            {
                Kind = EllenActivationKind.Launch,
                LaunchActivatedArguments = string.Empty,
                IsRedirectTo = true,
            };
        }

        using NamedPipeClientStream clientStream = new(".", PrivateNamedPipe.PrivateName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);

        try
        {
            clientStream.Connect(TimeSpan.Zero);
        }
        catch (TimeoutException)
        {
            return false;
        }

        try
        {
            clientStream.WritePacket(PrivateNamedPipe.PrivateVersion, PipePacketType.Request, PipePacketCommand.RequestElevationStatus);
            clientStream.ReadPacket(out PipePacketHeader _, out ElevationStatusResponse? response);
            ArgumentNullException.ThrowIfNull(response);

            if (EllenRuntime.IsProcessElevated && !response.IsElevated)
            {
                clientStream.WritePacket(PrivateNamedPipe.PrivateVersion, PipePacketType.SessionTermination, PipePacketCommand.Exit);
                clientStream.Flush();
                return false;
            }

            clientStream.WritePacketWithJsonContent(PrivateNamedPipe.PrivateVersion, PipePacketType.Request, PipePacketCommand.RedirectActivation, ellenArgs);
            clientStream.WritePacket(PrivateNamedPipe.PrivateVersion, PipePacketType.SessionTermination, PipePacketCommand.None);
            clientStream.Flush();

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        clientStream.Dispose();
    }

    private static void WaitPreviousProcessExit(ElevationStatusResponse response)
    {
        if (!ProcessFactory.TryGetById(response.ProcessId, out IProcess? process))
        {
            return;
        }

        if (process is { HasExited: false })
        {
            process.SafeWaitForExit();
        }

        SpinWaitPolyfill.SpinUntil(response, static response => !ProcessFactory.TryGetById(response.ProcessId, out _));
    }
}