// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Microsoft.Extensions.Caching.Memory;
using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Service.Git;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.CompilerServices;

namespace Ellen.Launcher.Service.Metadata;

[Service(ServiceLifetime.Singleton, typeof(IMetadataService))]
[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class MetadataService : IMetadataService
{
    private TaskCompletionSource initializeCompletionSource = new();

    private readonly IGitRepositoryService gitRepositoryService;
    private readonly ILogger<MetadataService> logger;
    private readonly MetadataOptions metadataOptions;
    private readonly JsonSerializerOptions options;

    private volatile bool isInitialized;

    [GeneratedConstructor]
    public partial MetadataService(IServiceProvider serviceProvider);

    public partial IMemoryCache MemoryCache { get; }

    public async ValueTask<bool> InitializeAsync()
    {
        await initializeCompletionSource.Task.ConfigureAwait(false);
        return isInitialized;
    }

    public async ValueTask<bool> InitializepublicAsync(CancellationToken token = default)
    {
        if (isInitialized)
        {
            return true;
        }

        using (ValueStopwatch.MeasureExecution(logger))
        {
            // If a previous attempt completed the TCS (with failure), create a new one
            // so callers of InitializeAsync() can properly wait for this retry.
            if (initializeCompletionSource.Task.IsCompleted)
            {
                initializeCompletionSource = new();
            }

            // Fast path: metadata for the current locale is already readable, either from the git
            // repository or from the snapshot shipped with the application. Skip both the API
            // round-trip and the fetch so that every page can load immediately, then refresh the
            // repository in the background.
            if (metadataOptions.HasUsableLocalizedData)
            {
                isInitialized = true;
                initializeCompletionSource.TrySetResult();
                gitRepositoryService.EnsureRepositoryInBackground("Snap.Metadata");
                return true;
            }

            (isInitialized, _) = await gitRepositoryService.EnsureRepositoryAsync("Snap.Metadata").ConfigureAwait(false);

            if (isInitialized)
            {
                initializeCompletionSource.TrySetResult();
            }

            return isInitialized;
        }
    }

    public async ValueTask<ImmutableArray<T>> FromCacheOrFileAsync<T>(MetadataFileStrategy strategy, CancellationToken token)
        where T : class
    {
        Verify.Operation(isInitialized, SH.ServiceMetadataNotInitialized);
        string cacheKey = $"{nameof(MetadataService)}.Cache.{strategy.Name}";

        if (MemoryCache.TryGetValue(cacheKey, out object? value))
        {
            ArgumentNullException.ThrowIfNull(value);
            return Unsafe.Unbox<ImmutableArray<T>>(value);
        }

        return strategy.IsScattered
            ? await FromCacheOrScatteredFileAsync<T>(strategy, cacheKey, token).ConfigureAwait(false)
            : await FromCacheOrSingleFileAsync<T>(strategy, cacheKey, token).ConfigureAwait(false);
    }

    private async ValueTask<ImmutableArray<T>> FromCacheOrSingleFileAsync<T>(MetadataFileStrategy strategy, string cacheKey, CancellationToken token)
        where T : class
    {
        string path = metadataOptions.GetLocalizedLocalPath($"{strategy.Name}.json");
        if (!File.Exists(path))
        {
            FileNotFoundException exception = new(SH.ServiceMetadataFileNotFound, strategy.Name);
            throw EllenException.Throw(SH.ServiceMetadataFileNotFound, exception);
        }

        using (Stream fileStream = File.OpenRead(path))
        {
            try
            {
                ImmutableArray<T> result = await JsonSerializer.DeserializeAsync<ImmutableArray<T>>(fileStream, options, token).ConfigureAwait(false);
                return MemoryCache.Set(cacheKey, result);
            }
            catch (Exception ex)
            {
                ex.Data.Add("FileName", strategy.Name);
                throw;
            }
        }
    }

    private async ValueTask<ImmutableArray<T>> FromCacheOrScatteredFileAsync<T>(MetadataFileStrategy strategy, string cacheKey, CancellationToken token)
        where T : class
    {
        string path = metadataOptions.GetLocalizedLocalPath(strategy.Name);
        if (!Directory.Exists(path))
        {
            DirectoryNotFoundException exception = new(SH.ServiceMetadataFileNotFound);
            throw EllenException.Throw(SH.ServiceMetadataFileNotFound, exception);
        }

        ImmutableArray<T>.Builder results = ImmutableArray.CreateBuilder<T>();
        foreach (string file in Directory.GetFiles(path, "*.json"))
        {
            string fileName = $"{strategy.Name}/{Path.GetFileNameWithoutExtension(file)}";
            using (Stream fileStream = File.OpenRead(file))
            {
                try
                {
                    T? result = await JsonSerializer.DeserializeAsync<T>(fileStream, options, token).ConfigureAwait(false);
                    ArgumentNullException.ThrowIfNull(result);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    ex.Data.Add("FileName", fileName);
                    throw;
                }
            }
        }

        return MemoryCache.Set(cacheKey, results.ToImmutable());
    }
}