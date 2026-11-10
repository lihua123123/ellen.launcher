// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.Caching;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Net.Http;

namespace Ellen.Launcher.Service.GachaLog;

/// <summary>
/// 从上游 Snap.Metadata 抓取最新的卡池事件与卡池图，写入本地缓存。
/// 只有新版本卡池才需要这份补充数据，因此按 <see cref="RefreshInterval"/> 节流；
/// 任何失败都只记录日志并继续使用随包快照，绝不影响启动与祈愿页。
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IGachaEventAutoUpdater))]
public sealed partial class GachaEventAutoUpdater : IGachaEventAutoUpdater
{
    private const string UpstreamUrl = "https://raw.githubusercontent.com/SnapHutaoRemasteringProject/Snap.Metadata/main/Genshin/CHS/GachaEvent.json";

    /// <summary>
    /// 只保留近 <see cref="KeepDays"/> 天的卡池，旧条目继续用随包快照，避免缓存文件无限增长。
    /// </summary>
    private const int KeepDays = 180;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);

    private static readonly HttpClient DirectClient = CreateClient(useProxy: false);

    /// <summary>
    /// 系统代理常常对 GitHub 域名不可用（或代理本身未启动），因此直连失败时再走一次系统代理。
    /// </summary>
    private static readonly HttpClient ProxiedClient = CreateClient(useProxy: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ILogger<GachaEventAutoUpdater> logger;
    private readonly IImageCache imageCache;
    private readonly SemaphoreSlim gate = new(1, 1);

    [GeneratedConstructor]
    public partial GachaEventAutoUpdater(IServiceProvider serviceProvider);

    private static string StampFilePath { get; } = Path.Combine(EllenRuntime.LocalCacheDirectory, "GachaEvent.extra.stamp");

    public async ValueTask UpdateInBackgroundAsync(CancellationToken token = default)
    {
        // 已有一次在跑就不再排队，避免多窗口 / 重试路径重复下载。
        if (!await gate.WaitAsync(0, token).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            if (!IsRefreshDue())
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            ImmutableArray<GachaEvent> fetched = await FetchRecentAsync(now, token).ConfigureAwait(false);
            if (fetched.IsEmpty)
            {
                return;
            }

            await MergeIntoOverrideAsync(fetched, token).ConfigureAwait(false);
            await PrecacheBannerImagesAsync(fetched, now, token).ConfigureAwait(false);
            await File.WriteAllTextAsync(StampFilePath, DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 启动流程被取消属于正常情况。
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "自动获取卡池素材失败，继续使用本地数据。");
        }
        finally
        {
            gate.Release();
        }
    }

    private static bool IsRefreshDue()
    {
        string path = StampFilePath;
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            string text = File.ReadAllText(path);
            return !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks)
                || DateTimeOffset.UtcNow - new DateTimeOffset(ticks, TimeSpan.Zero) >= RefreshInterval;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static async ValueTask<ImmutableArray<GachaEvent>> FetchRecentAsync(DateTimeOffset now, CancellationToken token)
    {
        // 先直连（本项目环境下 GitHub 直连可用、系统代理反而超时），失败再退回系统代理。
        string? json = await TryFetchJsonAsync(DirectClient, token).ConfigureAwait(false)
            ?? await TryFetchJsonAsync(ProxiedClient, token).ConfigureAwait(false);

        if (json is null)
        {
            return [];
        }

        List<GachaEvent>? events = JsonSerializer.Deserialize<List<GachaEvent>>(json, JsonOptions);
        if (events is null)
        {
            return [];
        }

        DateTimeOffset cutoff = now.AddDays(-KeepDays);
        return [.. events.Where(gachaEvent => gachaEvent.To >= cutoff).OrderBy(static gachaEvent => gachaEvent.From)];
    }

    private static async ValueTask<string?> TryFetchJsonAsync(HttpClient client, CancellationToken token)
    {
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, UpstreamUrl);
            request.Headers.UserAgent.ParseAdd(EllenRuntime.UserAgent);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    private static HttpClient CreateClient(bool useProxy)
    {
        SocketsHttpHandler handler = new()
        {
            UseProxy = useProxy,
            ConnectTimeout = TimeSpan.FromSeconds(6),
        };

        HttpClient client = new(handler)
        {
            Timeout = TimeSpan.FromSeconds(12),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(EllenRuntime.UserAgent);
        return client;
    }

    private static async ValueTask MergeIntoOverrideAsync(ImmutableArray<GachaEvent> fetched, CancellationToken token)
    {
        ImmutableArray<GachaEvent> existing = await GachaEventSupplement.ReadOverrideAsync(token).ConfigureAwait(false);

        Dictionary<(string Version, uint Order, GachaType Type), GachaEvent> map = [];

        foreach (GachaEvent gachaEvent in existing)
        {
            map[(gachaEvent.Version, gachaEvent.Order, gachaEvent.Type)] = gachaEvent;
        }

        foreach (GachaEvent gachaEvent in fetched)
        {
            map[(gachaEvent.Version, gachaEvent.Order, gachaEvent.Type)] = gachaEvent;
        }

        await GachaEventSupplement.WriteOverrideAsync([.. map.Values.OrderBy(static gachaEvent => gachaEvent.From)], token).ConfigureAwait(false);
    }

    private async ValueTask PrecacheBannerImagesAsync(ImmutableArray<GachaEvent> events, DateTimeOffset now, CancellationToken token)
    {
        foreach (GachaEvent gachaEvent in events)
        {
            token.ThrowIfCancellationRequested();
            if (gachaEvent.From > now || now > gachaEvent.To)
            {
                continue;
            }

            await TryCacheAsync(gachaEvent.Banner, token).ConfigureAwait(false);
            if (gachaEvent.Banner2 is { } second)
            {
                await TryCacheAsync(second, token).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask TryCacheAsync(Uri uri, CancellationToken token)
    {
        try
        {
            await imageCache.GetFileFromCacheAsync(uri).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 卡池图缺失不影响数据本身，UI 会在需要时再尝试一次。
            logger.LogDebug(ex, "卡池图预缓存失败：{Uri}", uri);
        }
    }
}
