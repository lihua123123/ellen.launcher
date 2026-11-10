// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Core.Setting;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 从自建数据站同步元数据快照（见 <see cref="IWikiMetadataSyncService"/>）。
///
/// 协议（对应 ellen-wiki 的 <c>scripts/build-api.mjs</c>）：
/// <list type="number">
///   <item>GET <c>/api/launcher/manifest.json</c> → <c>{ digest, generatedAt, archive: { path, size, sha256 } }</c></item>
///   <item>digest 与本地记录一致时直接返回，不下载</item>
///   <item>GET <c>/api/launcher/launcher-metadata.zip</c> → 校验大小与 sha256</item>
///   <item>解压到临时目录 → 写 <c>Meta.json</c> 哨兵 → 目录整体替换（避免半包状态）</item>
/// </list>
///
/// 同步目录放在 <see cref="EllenRuntime.LocalCacheDirectory"/> 下，**不能**放进
/// <c>Repository/</c>：那里有 12 小时一次的 git Reset(Hard) + RemoveUntrackedFiles，
/// 会把同步下来的文件清掉。
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IWikiMetadataSyncService))]
[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class WikiMetadataSyncService : IWikiMetadataSyncService
{
    private const string DefaultEndpoint = "https://ellen.wiki";
    private const string ManifestPath = "/api/launcher/manifest.json";
    private const string ArchiveFolder = "/api/launcher/";

    /// <summary>与内置快照一致的可用性哨兵文件名。</summary>
    private const string SentinelFileName = "Meta.json";

    private static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ArchiveTimeout = TimeSpan.FromMinutes(5);

    /// <summary>后台同步的最小间隔：启动频繁时不必每次都问一次 manifest。</summary>
    private static readonly TimeSpan BackgroundThrottle = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<WikiMetadataSyncService> logger;
    private readonly CultureOptions cultureOptions;
    private readonly SemaphoreSlim syncGate = new(1, 1);

    private long lastBackgroundAttemptUnix;
    private int backgroundRunning;

    [GeneratedConstructor]
    public partial WikiMetadataSyncService(IServiceProvider serviceProvider);

    public string Endpoint
    {
        get
        {
            /* 本地联调：指向 wrangler dev / 内网镜像，不用改设置库 */
            string fromEnvironment = Environment.GetEnvironmentVariable("ELLEN_WIKI_ENDPOINT");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
            {
                return fromEnvironment.TrimEnd('/');
            }

            string configured = LocalSetting.Get(SettingKeys.WikiMetadataEndpoint, DefaultEndpoint);
            return string.IsNullOrWhiteSpace(configured) ? DefaultEndpoint : configured.TrimEnd('/');
        }
    }

    public string SyncedLocalizedDataFolder => ResolveSyncedFolder();

    public bool HasSyncedData => File.Exists(Path.Combine(ResolveSyncedFolder(), SentinelFileName));

    public void SyncInBackground()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - Interlocked.Read(ref lastBackgroundAttemptUnix) < (long)BackgroundThrottle.TotalSeconds)
        {
            return;
        }

        if (Interlocked.Exchange(ref backgroundRunning, 1) == 1)
        {
            return;
        }

        Interlocked.Exchange(ref lastBackgroundAttemptUnix, now);
        _ = Task.Run(async () =>
        {
            try
            {
                await SyncAsync(false, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "后台同步元数据失败");
            }
            finally
            {
                Interlocked.Exchange(ref backgroundRunning, 0);
            }
        });
    }

    public async ValueTask<bool> SyncAsync(bool force, CancellationToken token)
    {
        await syncGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            WikiManifest? manifest = await GetManifestAsync(token).ConfigureAwait(false);
            if (manifest is null || string.IsNullOrEmpty(manifest.Digest) || manifest.Archive is null)
            {
                logger.LogDebug("数据站没有返回有效的 manifest，跳过同步");
                return false;
            }

            if (!force && HasSyncedData
                && string.Equals(ReadStateDigest(), manifest.Digest, StringComparison.Ordinal))
            {
                return false;
            }

            byte[] payload = await DownloadArchiveAsync(manifest.Archive, token).ConfigureAwait(false);
            if (payload.Length == 0)
            {
                return false;
            }

            ReplaceSnapshot(payload, manifest);
            WriteStateDigest(manifest.Digest);

            logger.LogInformation(
                "已同步元数据快照：{Avatars} 角色 / {Weapons} 武器（digest {Digest}）",
                manifest.Counts?.Avatar ?? 0,
                manifest.Counts?.Weapon ?? 0,
                manifest.Digest[..Math.Min(12, manifest.Digest.Length)]);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "同步元数据快照失败，继续使用现有数据");
            return false;
        }
        finally
        {
            syncGate.Release();
        }
    }

    /// <summary>
    /// 同步目录。<c>CultureOptions</c> 初始化完成前 <see cref="CultureOptions.LocaleName"/> 可能是空，
    /// 这里统一在调用时计算，避免构造期拿到错误的语言。
    /// </summary>
    private string ResolveSyncedFolder()
    {
        string locale = string.IsNullOrWhiteSpace(cultureOptions.LocaleName) ? "CHS" : cultureOptions.LocaleName;
        return Path.Combine(EllenRuntime.LocalCacheDirectory, "Metadata", "Wiki", "Genshin", locale);
    }

    private string ResolveStateFile()
    {
        return Path.ChangeExtension(ResolveSyncedFolder(), ".state.json");
    }

    private async ValueTask<WikiManifest?> GetManifestAsync(CancellationToken token)
    {
        Uri url = new(new Uri(Endpoint), ManifestPath);
        using HttpClient httpClient = httpClientFactory.CreateClient(nameof(WikiMetadataSyncService));
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", EllenRuntime.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(ManifestTimeout);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogDebug("获取 manifest 失败：HTTP {Status}", (int)response.StatusCode);
            return null;
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        return await JsonSerializer
            .DeserializeAsync<WikiManifest>(stream, ManifestJsonOptions, cts.Token)
            .ConfigureAwait(false);
    }

    private async ValueTask<byte[]> DownloadArchiveAsync(WikiArchive archive, CancellationToken token)
    {
        Uri url = new(new Uri(Endpoint), ArchiveFolder + archive.Path.TrimStart('/'));
        using HttpClient httpClient = httpClientFactory.CreateClient(nameof(WikiMetadataSyncService));
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", EllenRuntime.UserAgent);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(ArchiveTimeout);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cts.Token).ConfigureAwait(false);
        if (archive.Size > 0 && bytes.Length != archive.Size)
        {
            logger.LogWarning("元数据包大小不符：期望 {Expected}，实际 {Actual}", archive.Size, bytes.Length);
            return [];
        }

        if (!string.IsNullOrEmpty(archive.Sha256))
        {
            string actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actual, archive.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("元数据包 sha256 校验失败：期望 {Expected}，实际 {Actual}", archive.Sha256, actual);
                return [];
            }
        }

        return bytes;
    }

    private void ReplaceSnapshot(byte[] payload, WikiManifest manifest)
    {
        string target = ResolveSyncedFolder();
        string temporary = target + ".tmp";

        DeleteDirectory(temporary);
        Directory.CreateDirectory(temporary);

        using (MemoryStream stream = new(payload, writable: false))
        using (ZipArchive zip = new(stream, ZipArchiveMode.Read))
        {
            zip.ExtractToDirectory(temporary, overwriteFiles: true);
        }

        /* 哨兵：MetadataOptions 与内置快照用同一套判断（只检查存在性） */
        WikiSnapshotMarker marker = new()
        {
            Source = Endpoint,
            Digest = manifest.Digest,
            GeneratedAt = manifest.GeneratedAt,
            SyncedAt = DateTimeOffset.UtcNow,
            Avatars = manifest.Counts?.Avatar ?? 0,
            Weapons = manifest.Counts?.Weapon ?? 0,
        };
        File.WriteAllText(Path.Combine(temporary, SentinelFileName), JsonSerializer.Serialize(marker, ManifestJsonOptions));

        DeleteDirectory(target);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(temporary, target);

        /* 新快照带了新的 Assets.json，让图标索引立刻生效，不必重启 */
        Web.Endpoint.Upstream.LauncherAssetMirror.Invalidate();
    }

    private static void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        /* 元数据文件是按需打开的，正常不会被占用；个别杀软/索引器会短暂持有句柄，重试几次 */
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(120);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(120);
            }
        }
    }

    private string? ReadStateDigest()
    {
        try
        {
            string path = ResolveStateFile();
            if (!File.Exists(path))
            {
                return null;
            }

            WikiSyncState? state = JsonSerializer.Deserialize<WikiSyncState>(File.ReadAllText(path), ManifestJsonOptions);
            return state?.Digest;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void WriteStateDigest(string digest)
    {
        try
        {
            string path = ResolveStateFile();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WikiSyncState state = new() { Digest = digest, SyncedAt = DateTimeOffset.UtcNow };
            File.WriteAllText(path, JsonSerializer.Serialize(state, ManifestJsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "写入同步状态失败（不影响下一次 digest 比对之外的逻辑）");
        }
    }

    private sealed class WikiManifest
    {
        public string Digest { get; set; } = string.Empty;

        public DateTimeOffset GeneratedAt { get; set; }

        public WikiArchive? Archive { get; set; }

        public WikiCounts? Counts { get; set; }
    }

    private sealed class WikiArchive
    {
        public string Path { get; set; } = string.Empty;

        public long Size { get; set; }

        public string Sha256 { get; set; } = string.Empty;
    }

    private sealed class WikiCounts
    {
        public int Avatar { get; set; }

        public int Weapon { get; set; }
    }

    private sealed class WikiSyncState
    {
        public string Digest { get; set; } = string.Empty;

        public DateTimeOffset SyncedAt { get; set; }
    }

    private sealed class WikiSnapshotMarker
    {
        public string Source { get; set; } = string.Empty;

        public string Digest { get; set; } = string.Empty;

        public DateTimeOffset GeneratedAt { get; set; }

        public DateTimeOffset SyncedAt { get; set; }

        public int Avatars { get; set; }

        public int Weapons { get; set; }
    }
}
