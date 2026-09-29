// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Core.IO.Compression.Zstandard;
using Ellen.Launcher.Core.IO.Hashing;
using Ellen.Launcher.Core.Threading.RateLimiting;
using Ellen.Launcher.Factory.IO;
using Ellen.Launcher.Factory.Progress;
using Ellen.Launcher.Service.Game.FileSystem;
using Ellen.Launcher.Service.Game.Package.Advanced.Model;
using Ellen.Launcher.Service.Game.Package.Advanced.PackageOperation;
using Ellen.Launcher.Service.Notification;
using Ellen.Launcher.UI.Xaml.View.Window;
using Ellen.Launcher.Web.Hoyolab.Downloader;
using Ellen.Launcher.Web.Hoyolab.HoyoPlay.Connect.Branch;
using Ellen.Launcher.Web.Hoyolab.Takumi.Downloader.Proto;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Response;
using System.Collections.Immutable;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Ellen.Launcher.Service.Game.Package.Advanced;

[Service(ServiceLifetime.Singleton, typeof(IGamePackageService))]
[SuppressMessage("", "CA1001")]
[SuppressMessage("", "SA1201")]
[SuppressMessage("", "SA1204")]
public sealed partial class GamePackageService : IGamePackageService
{
    public const string HttpClientName = "SophonChunkRateLimited";

    private readonly GamePackageServiceOperationInformationTraits informationTraits;
    private readonly IMemoryStreamFactory memoryStreamFactory;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IServiceProvider serviceProvider;
    private readonly object operationStateLock = new();

    private CancellationTokenSource? operationCts;
    private TaskCompletionSource? operationTcs;
    private TaskCompletionSource? continueTcs;
    private AsyncManualResetEvent? operationResumeEvent;

    [GeneratedConstructor]
    public partial GamePackageService(IServiceProvider serviceProvider);

    public async ValueTask<bool> ExecuteOperationAsync(GamePackageOperationContext operationContext)
    {
        await StopOperationAsync().ConfigureAwait(false);

        CancellationTokenSource operationCtsLocal = new();
        TaskCompletionSource operationTcsLocal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource continueTcsLocal = new(TaskCreationOptions.RunContinuationsAsynchronously);
        AsyncManualResetEvent operationResumeEventLocal = new();
        operationResumeEventLocal.Set();

        lock (operationStateLock)
        {
            operationCts = operationCtsLocal;
            operationTcs = operationTcsLocal;
            continueTcs = continueTcsLocal;
            operationResumeEvent = operationResumeEventLocal;
        }

        ParallelOptions options = new()
        {
            CancellationToken = operationCtsLocal.Token,
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };

        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            ITaskContext taskContext = scope.ServiceProvider.GetRequiredService<ITaskContext>();

            if (await informationTraits.EnsureAvailableFreeSpaceAndPrepareAsync(operationContext).ConfigureAwait(false) is not { } info)
            {
                return false;
            }

            await taskContext.SwitchToMainThreadAsync();

            // TODO: The download progress window was removed together with the game-launching feature.
            //       Progress is reported headlessly; hook an InfoBar based reporter if UI feedback is needed.
            IProgress<GamePackageOperationReport> progress = scope.ServiceProvider
                .GetRequiredService<IProgressFactory>()
                .CreateForMainThread<GamePackageOperationReport>(static _ => { });

            await taskContext.SwitchToBackgroundAsync();

            bool result = false;
            using (HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName))
            using (TokenBucketRateLimiter? limiter = StreamCopyRateLimiter.Create(serviceProvider))
            {
                IGamePackageOperation operation = scope.ServiceProvider.GetRequiredKeyedService<IGamePackageOperation>(operationContext.Kind);
                GamePackageServiceContext serviceContext = new(operationContext, info, progress, options, httpClient, limiter, operationResumeEventLocal);

                try
                {
                    while (true)
                    {
                        try
                        {
                            await operation.ExecuteAsync(serviceContext).ConfigureAwait(false);
                            result = true;
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            if (operationCtsLocal.IsCancellationRequested)
                            {
                                serviceProvider.GetRequiredService<IMessenger>().Send(InfoBarMessage.Warning(SH.ServicePackageAdvancedExecuteOperationCanceledTitle));
                                return false;
                            }

                            throw;
                        }
                        catch (Exception ex)
                        {
                            if (ex is HttpRequestException httpRequestException && HttpRequestExceptionHandling.HttpRequestExceptionToNetworkError(httpRequestException) is Web.NetworkError.NULL)
                            {
                                SentrySdk.CaptureException(ex);
                            }

                            StringBuilder messageBuilder = new();
                            if (!HttpRequestExceptionHandling.FormatException(messageBuilder, ex, null))
                            {
                                messageBuilder.AppendLine(ex.Message);
                            }

                            progress.Report(new GamePackageOperationReport.RetryableFailure(messageBuilder.ToString().Trim()));

                            Task retryTask = continueTcsLocal.Task;
                            Task cancelTask = Task.Delay(Timeout.Infinite, operationCtsLocal.Token);
                            Task completedTask = await Task.WhenAny(retryTask, cancelTask).ConfigureAwait(false);
                            if (completedTask != retryTask)
                            {
                                result = false;
                                break;
                            }

                            continueTcsLocal = new(TaskCreationOptions.RunContinuationsAsynchronously);
                            lock (operationStateLock)
                            {
                                continueTcs = continueTcsLocal;
                            }
                        }
                    }
                }
                finally
                {
                    operationTcsLocal.TrySetResult();
                    operationCtsLocal.Dispose();

                    lock (operationStateLock)
                    {
                        if (ReferenceEquals(operationCts, operationCtsLocal))
                        {
                            operationCts = null;
                        }

                        if (ReferenceEquals(operationTcs, operationTcsLocal))
                        {
                            operationTcs = null;
                        }

                        if (ReferenceEquals(continueTcs, continueTcsLocal))
                        {
                            continueTcs = null;
                        }

                        if (ReferenceEquals(operationResumeEvent, operationResumeEventLocal))
                        {
                            operationResumeEvent = null;
                        }
                    }
                }
            }

            return result;
        }
    }

    public ValueTask CancelOperationAsync()
    {
        lock (operationStateLock)
        {
            operationResumeEvent?.Reset();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask ContinueOperationAsync()
    {
        lock (operationStateLock)
        {
            operationResumeEvent?.Set();
            continueTcs?.TrySetResult();
        }

        return ValueTask.CompletedTask;
    }

    private async ValueTask StopOperationAsync()
    {
        CancellationTokenSource? operationCtsLocal;
        TaskCompletionSource? operationTcsLocal;

        lock (operationStateLock)
        {
            operationCtsLocal = operationCts;
            operationTcsLocal = operationTcs;
            operationResumeEvent?.Set();
        }

        if (operationCtsLocal is null || operationTcsLocal is null)
        {
            return;
        }

        await operationCtsLocal.CancelAsync().ConfigureAwait(false);
        await operationTcsLocal.Task.ConfigureAwait(false);
    }

    public async ValueTask<SophonDecodedBuild?> DecodeManifestsAsync(IGameFileSystemView gameFileSystem, BranchWrapper? branch, CancellationToken token = default)
    {
        if (branch is null)
        {
            return default;
        }

        SophonBuild? build;
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            Response<SophonBuild> response = await scope.ServiceProvider
                .GetRequiredService<IOverseaSupportFactory<ISophonClient>>()
                .Create(gameFileSystem.IsExecutableOversea)
                .GetBuildAsync(branch, token)
                .ConfigureAwait(false);
            if (!ResponseValidator.TryValidate(response, scope.ServiceProvider, out build))
            {
                return default;
            }
        }

        return await DecodeManifestsAsync(gameFileSystem, build, token).ConfigureAwait(false);
    }

    public async ValueTask<SophonDecodedBuild?> DecodeManifestsAsync(IGameFileSystemView gameFileSystem, SophonBuild? build, CancellationToken token = default)
    {
        if (build is null)
        {
            return default;
        }

        long downloadTotalBytes = 0L;
        long totalBytes = 0L;
        ImmutableArray<SophonDecodedManifest>.Builder decodedManifests = ImmutableArray.CreateBuilder<SophonDecodedManifest>();
        using (HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName))
        {
            foreach (SophonManifest sophonManifest in build.Manifests)
            {
                bool exclude = sophonManifest.MatchingField switch
                {
                    "game" => false,
                    "zh-cn" => !gameFileSystem.Audio.Chinese,
                    "en-us" => !gameFileSystem.Audio.English,
                    "ja-jp" => !gameFileSystem.Audio.Japanese,
                    "ko-kr" => !gameFileSystem.Audio.Korean,
                    _ => true,
                };

                if (exclude)
                {
                    continue;
                }

                downloadTotalBytes += sophonManifest.Stats.CompressedSize;
                totalBytes += sophonManifest.Stats.UncompressedSize;

                string manifestDownloadUrl = $"{sophonManifest.ManifestDownload.UrlPrefix}/{sophonManifest.Manifest.Id}?{sophonManifest.ManifestDownload.UrlSuffix}";
                try
                {
                    using (Stream rawManifestStream = await httpClient.GetStreamAsync(manifestDownloadUrl, token).ConfigureAwait(false))
                    {
                        using (ZstandardDecompressStream decompressor = new(rawManifestStream))
                        {
                            using (MemoryStream inMemoryManifestStream = await memoryStreamFactory.GetStreamAsync(decompressor).ConfigureAwait(false))
                            {
                                string manifestMd5 = await Hash.ToHexStringAsync(HashAlgorithmName.MD5, inMemoryManifestStream, token).ConfigureAwait(false);
                                if (manifestMd5.Equals(sophonManifest.Manifest.Checksum, StringComparison.OrdinalIgnoreCase))
                                {
                                    inMemoryManifestStream.Position = 0;
                                    decodedManifests.Add(new(sophonManifest.ChunkDownload.UrlPrefix, sophonManifest.ChunkDownload.UrlSuffix, SophonManifestProto.Parser.ParseFrom(inMemoryManifestStream)));
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return default;
                }
                catch (Exception ex)
                {
                    StringBuilder messageBuilder = new();
                    if (HttpRequestExceptionHandling.FormatException(messageBuilder, ex, manifestDownloadUrl))
                    {
                        serviceProvider.GetRequiredService<IMessenger>().Send(InfoBarMessage.Error(messageBuilder.ToString(), ex));
                    }
                    else
                    {
                        // IOException: The request was aborted.
                        // + IOException: Unable to read data from the transport connection: 远程主机强迫关闭了一个现有的连接。.
                        //   + SocketException | ConnectionReset: 远程主机强迫关闭了一个现有的连接。
                        SentrySdk.CaptureException(ex);
                    }

                    return default;
                }
            }
        }

        return new(build.Tag, downloadTotalBytes, totalBytes, decodedManifests.ToImmutable());
    }

    public async ValueTask<SophonDecodedPatchBuild?> DecodeDiffManifestsAsync(IGameFileSystemView gameFileSystem, BranchWrapper? branch, CancellationToken token = default)
    {
        if (branch is null)
        {
            return default;
        }

        if (!gameFileSystem.TryGetGameVersion(out string? version))
        {
            return default;
        }

        if (!branch.DiffTags.Contains(version))
        {
            return default;
        }

        SophonPatchBuild? build;
        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            Response<SophonPatchBuild> response = await scope.ServiceProvider
                .GetRequiredService<IOverseaSupportFactory<ISophonClient>>()
                .Create(gameFileSystem.IsExecutableOversea)
                .GetPatchBuildAsync(branch, token)
                .ConfigureAwait(false);
            if (!ResponseValidator.TryValidate(response, scope.ServiceProvider, out build))
            {
                return default;
            }
        }

        return await DecodeDiffManifestsAsync(gameFileSystem, build, token).ConfigureAwait(false);
    }

    public async ValueTask<SophonDecodedPatchBuild?> DecodeDiffManifestsAsync(IGameFileSystemView gameFileSystem, SophonPatchBuild? patchBuild, CancellationToken token = default)
    {
        if (patchBuild is null)
        {
            return default;
        }

        if (!gameFileSystem.TryGetGameVersion(out string? version))
        {
            return default;
        }

        if (patchBuild.Manifests.Any(m => !m.Stats.ContainsKey(version)))
        {
            return default;
        }

        long downloadTotalBytes = 0L;
        long downloadFileCount = 0L;
        long totalBytes = 0L;
        long installFileCount = 0L;
        ImmutableArray<SophonDecodedPatchManifest>.Builder decodedPatchManifests = ImmutableArray.CreateBuilder<SophonDecodedPatchManifest>();
        using (HttpClient httpClient = httpClientFactory.CreateClient(HttpClientName))
        {
            foreach (SophonPatchManifest sophonPatchManifest in patchBuild.Manifests)
            {
                bool exclude = sophonPatchManifest.MatchingField switch
                {
                    "game" => false,
                    "zh-cn" => !gameFileSystem.Audio.Chinese,
                    "en-us" => !gameFileSystem.Audio.English,
                    "ja-jp" => !gameFileSystem.Audio.Japanese,
                    "ko-kr" => !gameFileSystem.Audio.Korean,
                    _ => true,
                };

                if (exclude)
                {
                    continue;
                }

                ManifestStats stats = sophonPatchManifest.Stats[version];
                downloadTotalBytes += stats.CompressedSize;
                downloadFileCount += stats.ChunkCount;
                totalBytes += stats.UncompressedSize;
                installFileCount += stats.FileCount;

                string manifestDownloadUrl = $"{sophonPatchManifest.ManifestDownload.UrlPrefix}/{sophonPatchManifest.Manifest.Id}?{sophonPatchManifest.ManifestDownload.UrlSuffix}";
                try
                {
                    using (Stream rawManifestStream = await httpClient.GetStreamAsync(manifestDownloadUrl, token).ConfigureAwait(false))
                    {
                        using (ZstandardDecompressStream decompressor = new(rawManifestStream))
                        {
                            using (MemoryStream inMemoryManifestStream = await memoryStreamFactory.GetStreamAsync(decompressor).ConfigureAwait(false))
                            {
                                string manifestMd5 = await Hash.ToHexStringAsync(HashAlgorithmName.MD5, inMemoryManifestStream, token).ConfigureAwait(false);
                                if (manifestMd5.Equals(sophonPatchManifest.Manifest.Checksum, StringComparison.OrdinalIgnoreCase))
                                {
                                    inMemoryManifestStream.Position = 0;
                                    decodedPatchManifests.Add(new(version, patchBuild.Tag, sophonPatchManifest.DiffDownload.UrlPrefix, sophonPatchManifest.DiffDownload.UrlSuffix, PatchManifest.Parser.ParseFrom(inMemoryManifestStream)));
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return default;
                }
            }
        }

        return new(version, patchBuild.Tag, downloadTotalBytes, downloadFileCount, totalBytes, installFileCount, decodedPatchManifests.ToImmutable());
    }
}
