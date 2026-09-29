// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Service.Game;
using Ellen.Launcher.Service.Yae.Achievement;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.Yae;

public sealed class YaeNamedPipeServer : IAsyncDisposable
{
    private const string PipeName = "YaeAchievementPipe";

    private readonly CancellationTokenSource disposeCts = new();
    private readonly AsyncLock disposeLock = new();

    private readonly TargetNativeConfiguration config;
    private readonly ITaskContext taskContext;
    private readonly IProcess gameProcess;

    private readonly NamedPipeServerStream serverStream;

    private volatile bool disposed;

    public YaeNamedPipeServer(IServiceProvider serviceProvider, IProcess gameProcess, TargetNativeConfiguration config)
    {
        Verify.Operation(EllenRuntime.IsProcessElevated, "Ellen Launcher must be elevated to use Yae.");

        taskContext = serviceProvider.GetRequiredService<ITaskContext>();

        this.gameProcess = gameProcess;
        this.config = config;

        serverStream = CreatePipeServerStream();
    }

    /// <summary>
    /// Creates the server side of the pipe that the injected Yae client connects to.
    /// </summary>
    /// <remarks>
    /// The pipe name is a fixed contract with the injected <c>YaeAchievementLib.dll</c>, and named pipe
    /// names are scoped to the machine instead of to this process. Any other launcher built from the same
    /// lineage therefore owns the very same name whenever it is running, and the injected client would
    /// connect to that one rather than to us. Report the conflict up front instead of failing later with a
    /// far more obscure error.
    /// </remarks>
    private static NamedPipeServerStream CreatePipeServerStream()
    {
        if (IsPipeNameInUse())
        {
            throw EllenException.InvalidOperation(SH.ServiceGameLaunchingHandlerEmbeddedYaePipeOccupied);
        }

        // Yae is always running elevated, so we don't need to use ACL method.
        return new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.None);
    }

    /// <summary>
    /// Determines whether some other process already published the pipe name we depend on.
    /// </summary>
    /// <remarks>
    /// The pipe namespace is machine wide, so enumerating it reveals owners from other processes, which a
    /// plain attempt to create our own instance cannot tell apart from a stale handle of our own.
    /// </remarks>
    private static bool IsPipeNameInUse()
    {
        try
        {
            return Directory.EnumerateFileSystemEntries(@"\\.\pipe\")
                .Any(path => string.Equals(Path.GetFileName(path), PipeName, StringComparison.Ordinal));
        }
        catch (IOException)
        {
            // Enumeration is best effort: if the namespace cannot be listed, let the creation below
            // surface any conflict instead of reporting a false positive.
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        await disposeCts.CancelAsync().ConfigureAwait(false);
        using (await disposeLock.LockAsync().ConfigureAwait(false))
        {
            // Discard
        }

        disposeCts.Dispose();
        await serverStream.DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask<ImmutableArray<YaeData>> GetDataArrayAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, "YaeNamedPipeServer is disposed.");

        using (await disposeLock.LockAsync().ConfigureAwait(false))
        {
            while (await GameLifeCycle.IsGameRunningAsync(taskContext).ConfigureAwait(false))
            {
                try
                {
                    using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(disposeCts.Token, token))
                    {
                        await serverStream.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);
                        return GetDataArray(serverStream);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        return default;
    }

    private unsafe YaeData? HandleCommand(BinaryReader reader, BinaryWriter writer, TargetNativeConfiguration config)
    {
        YaeCommandKind kind = reader.Read<YaeCommandKind>();

        switch (kind)
        {
            case YaeCommandKind.RequestCmdId:
                {
                    writer.Write(config.AchievementCmdId);
                    writer.Write(config.StoreCmdId);
                    return default;
                }

            case YaeCommandKind.RequestRva:
                {
                    writer.Write(config.DoCmd);
                    writer.Write(config.UpdateNormalProperty);
                    writer.Write(config.NewString);
                    writer.Write(config.FindGameObject);
                    writer.Write(config.EventSystemUpdate);
                    writer.Write(config.SimulatePointerClick);
                    writer.Write(config.ToInt32);
                    writer.Write(config.TcpStatePtr);
                    writer.Write(config.SharedInfoPtr);
                    writer.Write(config.Decompress);
                    return default;
                }

            case YaeCommandKind.RequestResumeThread:
                {
                    gameProcess.ResumeMainThread();
                    return default;
                }

            case YaeCommandKind.ResponseAchievement or YaeCommandKind.ResponsePlayerStore:
                {
                    int contentLength = reader.ReadInt32();
                    IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.RentExactly(contentLength);
                    reader.ReadExactly(owner.Memory.Span);
                    return new(kind, owner, contentLength);
                }

            case YaeCommandKind.ResponsePlayerProp:
                {
                    int contentLength = sizeof(YaePropertyTypeValue);
                    IMemoryOwner<byte> owner = MemoryPool<byte>.Shared.RentExactly(contentLength);
                    reader.ReadExactly(owner.Memory.Span);
                    return new(kind, owner, contentLength);
                }

            case YaeCommandKind.SessionEnd:
                {
                    writer.Write(true);
                    writer.Flush();
                    return YaeData.SessionEnd;
                }

            default:
                return default;
        }
    }

    private ImmutableArray<YaeData> GetDataArray(NamedPipeServerStream serverStream)
    {
        Debug.Assert(Thread.CurrentThread.IsBackground);

        // This method is never cancellable.
        // Yae will prompt error message if the server is closed.
        using BinaryReader reader = new(serverStream, Encoding.UTF8);
        using BinaryWriter writer = new(serverStream, Encoding.UTF8);

        ImmutableArray<YaeData>.Builder builder = ImmutableArray.CreateBuilder<YaeData>();
        while (gameProcess.IsRunning && serverStream.IsConnected)
        {
            try
            {
                // If command is SessionEnd, pipe client will close connection after HandleCommand.
                // Actually, yae exit after read the final bool value.
                if (HandleCommand(reader, writer, config) is { } data)
                {
                    if (data.Kind is YaeCommandKind.SessionEnd)
                    {
                        gameProcess.Kill();
                    }
                    else
                    {
                        builder.Add(data);
                    }
                }
            }
            catch (EndOfStreamException)
            {
                // Client closed.
            }
            catch (IOException)
            {
                // Pipe is broken.
            }
        }

        return builder.ToImmutable();
    }
}