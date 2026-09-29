// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Factory.Progress;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Launching.Context;
using Ellen.Launcher.Service.Game.Launching.Handler;
using Ellen.Launcher.Service.Game.Package;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Game.Launching.Invoker;

public abstract class AbstractLaunchExecutionInvoker
{
    private static readonly ConcurrentDictionary<AbstractLaunchExecutionInvoker, Void> Invokers = [];

    private bool invoked;

    protected ImmutableArray<ILaunchExecutionHandler> Handlers { get; init; }

    public static bool Invoking()
    {
        return !Invokers.IsEmpty;
    }

    public async ValueTask InvokeAsync(LaunchExecutionInvocationContext context)
    {
        EllenException.ThrowIf(Interlocked.Exchange(ref invoked, true), "The invoker has been invoked");
        ITaskContext taskContext = context.ServiceProvider.GetRequiredService<ITaskContext>();

        try
        {
            Invokers.TryAdd(this, default);
            await InvokeCoreAsync(context, taskContext).ConfigureAwait(false);
        }
        finally
        {
            Invokers.TryRemove(this, out _);
            if (!Invoking())
            {
                await GameLifeCycle.SpinWaitGameExitAsync(taskContext).ConfigureAwait(false);
            }
        }
    }

    protected virtual IProcess? CreateProcess(BeforeLaunchExecutionContext beforeContext)
    {
        return GameProcessFactory.CreateForDefault(beforeContext);
    }

    private static IProgress<LaunchStatus?> CreateStatusProgress(IServiceProvider serviceProvider)
    {
        IProgressFactory progressFactory = serviceProvider.GetRequiredService<IProgressFactory>();
        LaunchStatusOptions options = serviceProvider.GetRequiredService<LaunchStatusOptions>();
        return progressFactory.CreateForMainThread<LaunchStatus?, LaunchStatusOptions>(static (status, options) => options.LaunchStatus = status, options);
    }

    private async ValueTask InvokeCoreAsync(LaunchExecutionInvocationContext context, ITaskContext taskContext)
    {
        string lockTrace = $"{GetType().Name}.{nameof(InvokeAsync)}";
        context.LaunchOptions.TryGetGameFileSystem(lockTrace, out IGameFileSystem? gameFileSystem);
        ArgumentNullException.ThrowIfNull(gameFileSystem);

        using (GameFileSystemReference fileSystemReference = new(gameFileSystem))
        {
            if (context.ViewModel.TargetScheme is not { } targetScheme)
            {
                throw EllenException.InvalidOperation(SH.ViewModelLaunchGameSchemeNotSelected);
            }

            if (context.ViewModel.CurrentScheme is not { } currentScheme)
            {
                throw EllenException.InvalidOperation(SH.ServiceGameLaunchExecutionCurrentSchemeNull);
            }

            IProgress<LaunchStatus?> progress = CreateStatusProgress(context.ServiceProvider);

            BeforeLaunchExecutionContext beforeContext = new()
            {
                ViewModel = context.ViewModel,
                Progress = progress,
                ServiceProvider = context.ServiceProvider,
                TaskContext = taskContext,
                FileSystem = fileSystemReference,
                HoyoPlay = context.ServiceProvider.GetRequiredService<IHoyoPlayService>(),
                Messenger = context.ServiceProvider.GetRequiredService<IMessenger>(),
                LaunchOptions = context.LaunchOptions,
                CurrentScheme = currentScheme,
                TargetScheme = targetScheme,
                Identity = context.Identity,
            };

            foreach (ILaunchExecutionHandler handler in Handlers)
            {
                await handler.BeforeAsync(beforeContext).ConfigureAwait(false);
            }

            fileSystemReference.Exchange(beforeContext.FileSystem);

            using (IProcess? process = CreateProcess(beforeContext))
            {
                if (process is null)
                {
                    return;
                }

                LaunchExecutionContext executionContext = new()
                {
                    Progress = progress,
                    ServiceProvider = context.ServiceProvider,
                    TaskContext = taskContext,
                    Messenger = context.ServiceProvider.GetRequiredService<IMessenger>(),
                    LaunchOptions = context.LaunchOptions,
                    Process = process,
                    IsOversea = targetScheme.IsOversea,
                };

                foreach (ILaunchExecutionHandler handler in Handlers)
                {
                    await handler.ExecuteAsync(executionContext).ConfigureAwait(false);
                }

                if (process.IsRunning)
                {
                    progress.Report(new(SH.ServiceGameLaunchPhaseWaitingProcessExit));
                    try
                    {
                        await taskContext.SwitchToBackgroundAsync();
                        process.WaitForExit();
                    }
                    catch (Exception ex)
                    {
                        // Access denied, we are in non-elevated process
                        // Just leave and let invoker spin wait
                        SentrySdk.CaptureException(ex);
                        return;
                    }
                }
            }

            progress.Report(new(SH.ServiceGameLaunchPhaseProcessExited));

            AfterLaunchExecutionContext afterContext = new()
            {
                ServiceProvider = context.ServiceProvider,
                TaskContext = taskContext,
                LaunchOptions = context.LaunchOptions,
                Identity = context.Identity,
            };

            foreach (ILaunchExecutionHandler handler in Handlers)
            {
                await handler.AfterAsync(afterContext).ConfigureAwait(false);
            }
        }
    }
}