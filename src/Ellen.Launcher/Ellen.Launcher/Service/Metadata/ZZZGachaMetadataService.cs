// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.Model.Metadata.ZZZ;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 绝区零调频元数据的运行期服务。
///
/// 数据源：
/// 1. 物品（名称 / 图标 / 稀有度 / 类型）：Starward 公共静态元数据
/// 2. 档期 / UP / 版本：HoYo.Gacha 公共元数据 v2
/// 3. 卡池图：米游社官方公告搜索（best-effort，失败不影响主流程）
///
/// 首次访问时同步读取本地缓存或随包快照，过期时拉取一次；卡池图在后台补齐。
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IZZZGachaMetadataService))]
[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class ZZZGachaMetadataService : IZZZGachaMetadataService
{
    private const string ItemUrl = "https://starward-static.scighost.com/metadata/v1/zzz/ZZZGachaInfo.nap_cn.zh-cn.json";
    private const string IndexUrl = "https://hoyo-gacha-v1.lgou2w.com/GachaMetadata/v2/index.json";
    private const string DataUrlFormat = "https://hoyo-gacha-v1.lgou2w.com/GachaMetadata/v2/{0}.json";
    private const string BbsSearchUrlFormat = "https://bbs-api.miyoushe.com/post/wapi/searchPosts?keyword={0}&gids=8&size=50&last_id={1}";
    private const string BrowserUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Ellen.Launcher";

    private const int BbsMaxPages = 3;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(12);
    private static readonly TimeSpan RefreshAttemptInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AnnouncementAttemptInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CoreRequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AnnouncementRequestTimeout = TimeSpan.FromSeconds(20);

    private static readonly Regex VersionLimitedRegex = new(@"(\d+\.\d+)版本限时频段", RegexOptions.Compiled);
    private static readonly Regex VersionInSubjectRegex = new(@"(\d+\.\d+)版本", RegexOptions.Compiled);
    private static readonly Regex SubjectTitleRegex = new(@"「([^」]+)」", RegexOptions.Compiled);
    private static readonly Regex BannerTitleRegex = new(@"「([^」]+)」调频活动", RegexOptions.Compiled);
    private static readonly Regex SItemRegex = new(@"(?:限定)?S级(代理人|音擎)\[([^\(\[]+)", RegexOptions.Compiled);

    private static readonly (char Source, string Replacement)[] RomanNumerals =
    [
        ('Ⅰ', "I"),
        ('Ⅱ', "II"),
        ('Ⅲ', "III"),
        ('Ⅳ', "IV"),
        ('Ⅴ', "V"),
        ('Ⅵ', "VI"),
        ('Ⅶ', "VII"),
        ('Ⅷ', "VIII"),
        ('Ⅸ', "IX"),
        ('Ⅹ', "X"),
    ];

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<ZZZGachaMetadataService> logger;
    private readonly Lock gate = new();
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    private State? state;
    private long lastRefreshAttemptUnix;
    private long lastAnnouncementAttemptUnix;
    private int announcementRefreshRunning;

    [GeneratedConstructor]
    public partial ZZZGachaMetadataService(IServiceProvider serviceProvider);

    public event EventHandler? Updated;

    public ImmutableArray<ZZZGachaBanner> Banners => [.. EnsureState().Snapshot.Banners];

    public DateTimeOffset LastUpdated => EnsureState().Snapshot.FetchedAt;

    public ZZZGachaItemInfo? GetById(uint itemId)
    {
        return EnsureState().Items.TryGetValue(itemId, out ZZZGachaItemInfo? item) ? item : null;
    }

    public async ValueTask EnsureInitializedAsync(CancellationToken token = default)
    {
        State current = EnsureState();
        if (!IsStale(current.Snapshot))
        {
            QueueAnnouncementRefresh(current);
            return;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - Volatile.Read(ref lastRefreshAttemptUnix) < (long)RefreshAttemptInterval.TotalSeconds)
        {
            QueueAnnouncementRefresh(current);
            return;
        }

        await RefreshCoreAsync(token).ConfigureAwait(false);
        QueueAnnouncementRefresh(EnsureState());
    }

    private State EnsureState()
    {
        State? current = Volatile.Read(ref state);
        if (current is not null)
        {
            return current;
        }

        lock (gate)
        {
            current = state;
            if (current is not null)
            {
                return current;
            }

            current = CreateState(LoadLocalSnapshot());
            Volatile.Write(ref state, current);
            return current;
        }
    }

    private static State CreateState(ZZZGachaSnapshot snapshot)
    {
        FrozenDictionary<uint, ZZZGachaItemInfo> items = snapshot.Items
            .GroupBy(static item => item.Id)
            .ToFrozenDictionary(static group => group.Key, static group => group.Last());
        return new State(snapshot, items);
    }

    private void SetState(ZZZGachaSnapshot snapshot)
    {
        Volatile.Write(ref state, CreateState(snapshot));
    }

    private void RaiseUpdated()
    {
        try
        {
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ZZZ gacha metadata Updated subscriber threw.");
        }
    }

    private static bool IsStale(ZZZGachaSnapshot snapshot)
    {
        return snapshot.FetchedAt == DateTimeOffset.MinValue
            || DateTimeOffset.UtcNow - snapshot.FetchedAt >= RefreshInterval;
    }

    private async ValueTask RefreshCoreAsync(CancellationToken token)
    {
        Interlocked.Exchange(ref lastRefreshAttemptUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await refreshGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            State current = EnsureState();
            if (!IsStale(current.Snapshot))
            {
                return;
            }

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(12));

            bool itemsFetched = false;
            bool bannersFetched = false;
            List<ZZZGachaItemInfo> items = current.Snapshot.Items;
            List<ZZZGachaBanner> banners = current.Snapshot.Banners;
            string latest = current.Snapshot.Latest;

            try
            {
                items = await FetchItemsAsync(cts.Token).ConfigureAwait(false);
                itemsFetched = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to fetch ZZZ gacha item metadata.");
            }

            try
            {
                (banners, latest) = await FetchBannersAsync(items, current.Snapshot, cts.Token).ConfigureAwait(false);
                bannersFetched = true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to fetch ZZZ gacha banner metadata.");
            }

            if (!itemsFetched && !bannersFetched)
            {
                return;
            }

            DateTimeOffset fetchedAt = itemsFetched && bannersFetched
                ? DateTimeOffset.UtcNow
                : current.Snapshot.FetchedAt;

            ZZZGachaSnapshot snapshot = new()
            {
                Source = "upstream",
                Latest = latest,
                FetchedAt = fetchedAt,
                Items = items,
                Banners = banners,
            };

            SaveSnapshot(snapshot);
            SetState(snapshot);
            RaiseUpdated();
        }
        catch (OperationCanceledException)
        {
            if (!token.IsCancellationRequested)
            {
                logger.LogDebug("ZZZ gacha metadata refresh timed out.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refresh ZZZ gacha metadata.");
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task<List<ZZZGachaItemInfo>> FetchItemsAsync(CancellationToken token)
    {
        string json = await GetStringAsync(ItemUrl, CoreRequestTimeout, token).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out JsonElement data)
            || !data.TryGetProperty("list", out JsonElement list)
            || list.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidOperationException("Unexpected ZZZ gacha item payload.");
        }

        List<ZZZGachaItemInfo> items = [];
        foreach (JsonElement item in list.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out JsonElement idElement) || !idElement.TryGetUInt32(out uint id))
            {
                continue;
            }

            string name = item.TryGetProperty("name", out JsonElement nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
            string icon = item.TryGetProperty("icon", out JsonElement iconElement) ? iconElement.GetString() ?? string.Empty : string.Empty;
            int rarity = item.TryGetProperty("rarity", out JsonElement rarityElement) && rarityElement.TryGetInt32(out int parsedRarity)
                ? parsedRarity
                : 0;

            items.Add(new ZZZGachaItemInfo
            {
                Id = id,
                Name = name,
                Icon = icon,
                Rarity = rarity,
                Type = Classify(id, icon),
            });
        }

        if (items.Count is 0)
        {
            throw new InvalidOperationException("ZZZ gacha item payload is empty.");
        }

        items.Sort(static (left, right) => left.Id.CompareTo(right.Id));
        return items;
    }

    private async Task<(List<ZZZGachaBanner> Banners, string Latest)> FetchBannersAsync(List<ZZZGachaItemInfo> items, ZZZGachaSnapshot previous, CancellationToken token)
    {
        string indexJson = await GetStringAsync(IndexUrl, CoreRequestTimeout, token).ConfigureAwait(false);
        using JsonDocument index = JsonDocument.Parse(indexJson);
        if (!index.RootElement.TryGetProperty("Latest", out JsonElement latestElement))
        {
            throw new InvalidOperationException("Unexpected ZZZ gacha index payload.");
        }

        string latest = latestElement.GetString() ?? string.Empty;
        if (string.IsNullOrEmpty(latest))
        {
            throw new InvalidOperationException("ZZZ gacha index has no latest version.");
        }

        string dataJson = await GetStringAsync(
            string.Format(CultureInfo.InvariantCulture, DataUrlFormat, latest),
            CoreRequestTimeout,
            token).ConfigureAwait(false);

        using JsonDocument data = JsonDocument.Parse(dataJson);
        if (data.RootElement.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidOperationException("Unexpected ZZZ gacha data payload.");
        }

        JsonElement? nap = null;
        foreach (JsonElement game in data.RootElement.EnumerateArray())
        {
            if (game.TryGetProperty("Id", out JsonElement idElement)
                && idElement.TryGetInt32(out int gameId)
                && gameId is 2)
            {
                nap = game;
                break;
            }
        }

        if (nap is not JsonElement napElement
            || !napElement.TryGetProperty("Banners", out JsonElement bannerElements)
            || bannerElements.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidOperationException("ZZZ gacha metadata has no banners.");
        }

        List<BannerRow> rows = [];
        foreach (JsonElement row in bannerElements.EnumerateArray())
        {
            int type = row.TryGetProperty("GachaType", out JsonElement typeElement) && typeElement.TryGetInt32(out int parsedType)
                ? parsedType
                : 0;
            if (type is 0)
            {
                continue;
            }

            rows.Add(new BannerRow(
                type,
                ParseDateTimeOffset(row, "StartTime"),
                ParseDateTimeOffset(row, "EndTime"),
                ParseUInt32Array(row, "UpGolden"),
                ParseUInt32Array(row, "UpPurple"),
                row.TryGetProperty("Version", out JsonElement versionElement) ? versionElement.GetString() ?? string.Empty : string.Empty));
        }

        return (BuildBanners(rows, items, previous, null), latest);
    }

    private static List<ZZZGachaBanner> BuildBanners(
        List<BannerRow> rows,
        List<ZZZGachaItemInfo> items,
        ZZZGachaSnapshot previous,
        List<AnnouncementImage>? announcements)
    {
        Dictionary<uint, ZZZGachaItemInfo> itemById = items
            .GroupBy(static item => item.Id)
            .ToDictionary(static group => group.Key, static group => group.Last());

        Dictionary<(int Type, DateTimeOffset Start, uint Up), ZZZGachaBanner> existing = [];
        foreach (ZZZGachaBanner banner in previous.Banners)
        {
            if (banner.Up.Count > 0)
            {
                existing[(banner.Type, banner.Start, banner.Up[0])] = banner;
            }
        }

        Dictionary<uint, (string Image, string Name)> imagesByItem = [];
        foreach (ZZZGachaBanner banner in previous.Banners)
        {
            if (banner.Up.Count > 0 && !string.IsNullOrEmpty(banner.Image))
            {
                imagesByItem.TryAdd(banner.Up[0], (banner.Image, banner.Name));
            }
        }

        List<ZZZGachaBanner> banners = [];
        foreach (BannerRow row in rows)
        {
            string kind = row.Type is 2 or 102 ? "agent" : "engine";
            foreach (uint itemId in row.UpGolden.Distinct())
            {
                existing.TryGetValue((row.Type, row.Start, itemId), out ZZZGachaBanner? old);
                string name = old?.Name ?? string.Empty;
                string image = old?.Image ?? string.Empty;

                if (string.IsNullOrEmpty(image) && announcements is { Count: > 0 })
                {
                    string itemName = itemById.TryGetValue(itemId, out ZZZGachaItemInfo? info) ? info.Name : string.Empty;
                    AnnouncementImage? entry = PickEntry(announcements, kind, itemName, row.Version, row.Start);
                    if (entry is not null)
                    {
                        image = entry.Image;
                        if (string.IsNullOrEmpty(name))
                        {
                            name = entry.Title;
                        }
                    }
                }

                if (string.IsNullOrEmpty(image) && imagesByItem.TryGetValue(itemId, out (string Image, string Name) fallback))
                {
                    image = fallback.Image;
                    if (string.IsNullOrEmpty(name))
                    {
                        name = fallback.Name;
                    }
                }

                List<uint> upPurple = row.UpPurple.Count > 0 ? row.UpPurple : old?.UpPurple ?? [];
                string version = string.IsNullOrEmpty(row.Version) ? old?.Version ?? string.Empty : row.Version;

                banners.Add(new ZZZGachaBanner
                {
                    Type = row.Type,
                    Start = row.Start,
                    End = row.End,
                    Name = name,
                    Up = [itemId],
                    UpPurple = upPurple,
                    Version = version,
                    Image = image,
                });
            }
        }

        banners.Sort(static (left, right) =>
        {
            int result = left.Start.CompareTo(right.Start);
            if (result is not 0)
            {
                return result;
            }

            result = left.Type.CompareTo(right.Type);
            if (result is not 0)
            {
                return result;
            }

            uint leftUp = left.Up.Count > 0 ? left.Up[0] : 0;
            uint rightUp = right.Up.Count > 0 ? right.Up[0] : 0;
            return leftUp.CompareTo(rightUp);
        });

        return banners;
    }

    private void QueueAnnouncementRefresh(State current)
    {
        if (!NeedsAnnouncementRefresh(current.Snapshot))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref announcementRefreshRunning, 1, 0) is not 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await RefreshAnnouncementImagesAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to refresh ZZZ gacha banner images.");
            }
            finally
            {
                Volatile.Write(ref announcementRefreshRunning, 0);
            }
        });
    }

    private static bool NeedsAnnouncementRefresh(ZZZGachaSnapshot snapshot)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        return snapshot.Banners.Any(banner =>
            string.IsNullOrEmpty(banner.Image)
            && banner.Up.Count > 0
            && banner.Start >= now.AddDays(-45)
            && banner.Start <= now.AddDays(90));
    }

    private async ValueTask RefreshAnnouncementImagesAsync()
    {
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowUnix - Volatile.Read(ref lastAnnouncementAttemptUnix) < (long)AnnouncementAttemptInterval.TotalSeconds)
        {
            return;
        }

        Interlocked.Exchange(ref lastAnnouncementAttemptUnix, nowUnix);
        await refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            State current = EnsureState();
            DateTimeOffset now = DateTimeOffset.Now;
            List<ZZZGachaBanner> missing = [.. current.Snapshot.Banners.Where(banner =>
                string.IsNullOrEmpty(banner.Image)
                && banner.Up.Count > 0
                && banner.Start >= now.AddDays(-45)
                && banner.Start <= now.AddDays(90))];

            if (missing.Count is 0)
            {
                return;
            }

            string[] versions = [.. missing
                .Select(static banner => banner.Version)
                .Where(static version => !string.IsNullOrEmpty(version))
                .Distinct()
                .Take(2)];

            using CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));
            List<AnnouncementImage> entries = await CollectAnnouncementEntriesAsync(versions, cts.Token).ConfigureAwait(false);
            if (entries.Count is 0)
            {
                return;
            }

            List<ZZZGachaBanner> banners = [.. current.Snapshot.Banners];
            bool changed = false;
            DateTimeOffset currentNow = DateTimeOffset.Now;
            for (int index = 0; index < banners.Count; index++)
            {
                ZZZGachaBanner banner = banners[index];
                if (!string.IsNullOrEmpty(banner.Image) || banner.Up.Count is 0)
                {
                    continue;
                }

                if (banner.Start < currentNow.AddDays(-45) || banner.Start > currentNow.AddDays(90))
                {
                    continue;
                }

                string kind = banner.Type is 2 or 102 ? "agent" : "engine";
                string itemName = current.Items.TryGetValue(banner.Up[0], out ZZZGachaItemInfo? info) ? info.Name : string.Empty;
                AnnouncementImage? entry = PickEntry(entries, kind, itemName, banner.Version, banner.Start);
                if (entry is null)
                {
                    continue;
                }

                banners[index] = new ZZZGachaBanner
                {
                    Type = banner.Type,
                    Start = banner.Start,
                    End = banner.End,
                    Name = string.IsNullOrEmpty(banner.Name) ? entry.Title : banner.Name,
                    Up = banner.Up,
                    UpPurple = banner.UpPurple,
                    Version = banner.Version,
                    Image = entry.Image,
                };
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            ZZZGachaSnapshot snapshot = new()
            {
                Source = current.Snapshot.Source,
                Latest = current.Snapshot.Latest,
                FetchedAt = DateTimeOffset.UtcNow,
                Items = current.Snapshot.Items,
                Banners = banners,
            };

            SaveSnapshot(snapshot);
            SetState(snapshot);
            RaiseUpdated();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch ZZZ gacha banner images.");
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task<List<AnnouncementImage>> CollectAnnouncementEntriesAsync(IReadOnlyCollection<string> versions, CancellationToken token)
    {
        List<AnnouncementImage> entries = [];
        HashSet<string> seenImages = [];
        foreach (string keyword in new[] { "调频说明", "限时频段" })
        {
            List<JsonElement> posts = await SearchPostsAsync(keyword, token).ConfigureAwait(false);
            foreach (JsonElement post in posts)
            {
                if (!IsOfficialPost(post))
                {
                    continue;
                }

                string subject = post.TryGetProperty("subject", out JsonElement subjectElement) ? subjectElement.GetString() ?? string.Empty : string.Empty;
                if (!IsBannerAnnouncement(subject))
                {
                    // 「X版本更新说明」等公告的封面图后面也会跟 S 级物品名，
                    // 但不是卡池图，必须排除，否则会把版本封面当成卡池图。
                    continue;
                }

                string postVersion = ExtractVersion(subject);
                if (versions.Count > 0 && !string.IsNullOrEmpty(postVersion) && !versions.Contains(postVersion))
                {
                    continue;
                }

                string subjectTitle = SubjectTitleRegex.Match(subject).Groups[1].Value;
                long created = post.TryGetProperty("created_at", out JsonElement createdElement) && createdElement.TryGetInt64(out long parsedCreated)
                    ? parsedCreated
                    : 0;

                foreach (AnnouncementImage image in ExtractBannerImages(post))
                {
                    if (!seenImages.Add(image.Image))
                    {
                        continue;
                    }

                    entries.Add(new AnnouncementImage(
                        image.Kind,
                        image.Name,
                        image.Image,
                        string.IsNullOrEmpty(image.Title) ? subjectTitle : image.Title,
                        postVersion,
                        created));
                }
            }
        }

        return entries;
    }

    private async Task<List<JsonElement>> SearchPostsAsync(string keyword, CancellationToken token)
    {
        List<JsonElement> posts = [];
        long lastId = 0;
        HashSet<long> visited = [];
        for (int page = 0; page < BbsMaxPages; page++)
        {
            if (!visited.Add(lastId))
            {
                break;
            }

            string url = string.Format(
                CultureInfo.InvariantCulture,
                BbsSearchUrlFormat,
                Uri.EscapeDataString(keyword),
                lastId);

            string json = await GetStringAsync(url, AnnouncementRequestTimeout, token).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data))
            {
                break;
            }

            if (data.TryGetProperty("posts", out JsonElement postArray) && postArray.ValueKind is JsonValueKind.Array)
            {
                foreach (JsonElement wrapper in postArray.EnumerateArray())
                {
                    if (wrapper.TryGetProperty("post", out JsonElement post) && post.ValueKind is JsonValueKind.Object)
                    {
                        posts.Add(post.Clone());
                    }
                }
            }

            if (!data.TryGetProperty("last_id", out JsonElement lastIdElement) || !TryGetInt64(lastIdElement, out long nextId) || nextId is 0)
            {
                break;
            }

            lastId = nextId;
            await Task.Delay(200, token).ConfigureAwait(false);
        }

        return posts;
    }

    private static bool IsOfficialPost(JsonElement post)
    {
        return post.TryGetProperty("post_status", out JsonElement status)
            && status.TryGetProperty("is_official", out JsonElement official)
            && official.ValueKind is JsonValueKind.True;
    }

    private static bool IsBannerAnnouncement(string subject)
    {
        return subject.Contains("限时频段", StringComparison.Ordinal)
            || subject.Contains("调频说明", StringComparison.Ordinal)
            || subject.Contains("调频活动", StringComparison.Ordinal);
    }

    private static string ExtractVersion(string subject)
    {
        Match limited = VersionLimitedRegex.Match(subject);
        if (limited.Success)
        {
            return limited.Groups[1].Value;
        }

        Match version = VersionInSubjectRegex.Match(subject);
        return version.Success ? version.Groups[1].Value : string.Empty;
    }

    private static List<AnnouncementImage> ExtractBannerImages(JsonElement post)
    {
        if (!post.TryGetProperty("structured_content", out JsonElement structuredElement)
            || structuredElement.ValueKind is not JsonValueKind.String)
        {
            return [];
        }

        string? content = structuredElement.GetString();
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        using JsonDocument structured = JsonDocument.Parse(content);
        if (structured.RootElement.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        List<(bool IsImage, string Value)> sequence = [];
        foreach (JsonElement entry in structured.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("insert", out JsonElement insert))
            {
                continue;
            }

            if (insert.ValueKind is JsonValueKind.String)
            {
                sequence.Add((false, insert.GetString() ?? string.Empty));
            }
            else if (insert.ValueKind is JsonValueKind.Object && insert.TryGetProperty("image", out _))
            {
                sequence.Add((true, string.Empty));
            }
        }

        List<string> images = [];
        if (post.TryGetProperty("images", out JsonElement imageArray) && imageArray.ValueKind is JsonValueKind.Array)
        {
            foreach (JsonElement image in imageArray.EnumerateArray())
            {
                if (image.ValueKind is JsonValueKind.String)
                {
                    images.Add(image.GetString() ?? string.Empty);
                }
            }
        }

        List<AnnouncementImage> results = [];
        int imageIndex = 0;
        string buffer = string.Empty;
        for (int index = 0; index < sequence.Count; index++)
        {
            (bool isImage, string value) = sequence[index];
            if (!isImage)
            {
                buffer += value;
                continue;
            }

            string imageUrl = imageIndex < images.Count ? images[imageIndex] : string.Empty;
            imageIndex++;

            string after = string.Empty;
            for (int next = index + 1; next < sequence.Count; next++)
            {
                if (sequence[next].IsImage)
                {
                    break;
                }

                after += sequence[next].Value;
            }

            Match match = SItemRegex.Match(after);
            if (match.Success && !string.IsNullOrEmpty(imageUrl))
            {
                string title = BannerTitleRegex.Match(buffer).Groups[1].Value;
                string kind = match.Groups[1].Value is "代理人" ? "agent" : "engine";
                results.Add(new AnnouncementImage(kind, match.Groups[2].Value.Trim(), imageUrl, title, string.Empty, 0));
            }

            buffer = string.Empty;
        }

        return results;
    }

    private static AnnouncementImage? PickEntry(
        List<AnnouncementImage> entries,
        string kind,
        string itemName,
        string version,
        DateTimeOffset start)
    {
        AnnouncementImage? best = null;
        int bestScore = 0;
        foreach (AnnouncementImage entry in entries)
        {
            if (entry.Kind != kind)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(version) && !string.IsNullOrEmpty(entry.Version) && entry.Version != version)
            {
                continue;
            }

            int score = MatchScore(itemName, entry.Name);
            if (score is 0)
            {
                continue;
            }

            score = (score * 10) + (IsNearStart(entry.Created, start) ? 3 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = entry;
            }
        }

        return best;
    }

    private static int MatchScore(string itemName, string entryName)
    {
        string left = NormalizeName(itemName);
        string right = NormalizeName(entryName);
        if (left.Length is 0 || right.Length is 0)
        {
            return 0;
        }

        if (left == right)
        {
            return 4;
        }

        if (right.StartsWith(left, StringComparison.Ordinal))
        {
            return 3;
        }

        if (right.Contains(left, StringComparison.Ordinal))
        {
            return 2;
        }

        if (left.Contains(right, StringComparison.Ordinal))
        {
            return 1;
        }

        return 0;
    }

    private static string NormalizeName(string value)
    {
        string result = value.Trim();
        foreach ((char source, string replacement) in RomanNumerals)
        {
            result = result.Replace(source.ToString(), replacement, StringComparison.Ordinal);
        }

        foreach (char noise in "「」『』·・ 　")
        {
            result = result.Replace(noise.ToString(), string.Empty, StringComparison.Ordinal);
        }

        return result.ToUpperInvariant();
    }

    private static bool IsNearStart(long createdUnix, DateTimeOffset start)
    {
        if (createdUnix <= 0)
        {
            return false;
        }

        DateTimeOffset created = DateTimeOffset.FromUnixTimeSeconds(createdUnix).ToOffset(start.Offset);
        return created >= start.AddDays(-10) && created <= start.AddDays(2);
    }

    private static string Classify(uint itemId, string icon)
    {
        if (icon.Contains("role_square_avatar", StringComparison.Ordinal))
        {
            return "agent";
        }

        if (itemId is >= 50_000 and < 60_000)
        {
            return "bangboo";
        }

        return "wengine";
    }

    private static DateTimeOffset ParseDateTimeOffset(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement value)
            && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset result)
                ? result
                : DateTimeOffset.MinValue;
    }

    private static List<uint> ParseUInt32Array(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement array) || array.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        List<uint> values = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.TryGetUInt32(out uint value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool TryGetInt64(JsonElement element, out long value)
    {
        if (element.ValueKind is JsonValueKind.Number)
        {
            return element.TryGetInt64(out value);
        }

        if (element.ValueKind is JsonValueKind.String)
        {
            return long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        value = 0;
        return false;
    }

    private async Task<string> GetStringAsync(string url, TimeSpan timeout, CancellationToken token)
    {
        using HttpClient httpClient = httpClientFactory.CreateClient(nameof(ZZZGachaMetadataService));
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(timeout);
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
    }

    private void SaveSnapshot(ZZZGachaSnapshot snapshot)
    {
        try
        {
            string path = CacheFilePath;
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = $"{path}.tmp";
            using (FileStream stream = File.Create(temporaryPath))
            {
                JsonSerializer.Serialize(stream, snapshot, ZZZGachaSnapshotJsonContext.Default.ZZZGachaSnapshot);
            }

            File.Move(temporaryPath, path, true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save ZZZ gacha metadata cache.");
        }
    }

    private ZZZGachaSnapshot LoadLocalSnapshot()
    {
        string path = CacheFilePath;
        if (File.Exists(path))
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                ZZZGachaSnapshot? snapshot = JsonSerializer.Deserialize(stream, ZZZGachaSnapshotJsonContext.Default.ZZZGachaSnapshot);
                if (snapshot is { Items.Count: > 0, Banners.Count: > 0 })
                {
                    return snapshot;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read ZZZ gacha metadata cache: {Path}", path);
            }
        }

        return LoadBundledSnapshot();
    }

    private ZZZGachaSnapshot LoadBundledSnapshot()
    {
        List<ZZZGachaBanner> banners = [];
        List<ZZZGachaItemInfo> items = [];

        try
        {
            string path = InstalledLocation.GetAbsolutePath("Resource/Metadata/ZZZ/GachaBanner.json");
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                ZZZGachaBannerDataset? dataset = JsonSerializer.Deserialize(stream, ZZZGachaBannerJsonContext.Default.ZZZGachaBannerDataset);
                if (dataset is not null)
                {
                    banners = dataset.Banners;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read bundled ZZZ gacha banners.");
        }

        try
        {
            string path = InstalledLocation.GetAbsolutePath("Resource/Metadata/ZZZ/GachaItem.json");
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                ZZZGachaItemInfoDataset? dataset = JsonSerializer.Deserialize(stream, ZZZGachaItemInfoJsonContext.Default.ZZZGachaItemInfoDataset);
                if (dataset is not null)
                {
                    items = dataset.Items;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read bundled ZZZ gacha items.");
        }

        return new ZZZGachaSnapshot
        {
            Source = "bundled",
            FetchedAt = DateTimeOffset.MinValue,
            Items = items,
            Banners = banners,
        };
    }

    private static string CacheFilePath => Path.Combine(EllenRuntime.GetDataServerCacheDirectory(), "ZZZ", "GachaMetadata.json");

    private sealed record State(ZZZGachaSnapshot Snapshot, FrozenDictionary<uint, ZZZGachaItemInfo> Items);

    private sealed record BannerRow(
        int Type,
        DateTimeOffset Start,
        DateTimeOffset End,
        List<uint> UpGolden,
        List<uint> UpPurple,
        string Version);

    private sealed record AnnouncementImage(
        string Kind,
        string Name,
        string Image,
        string Title,
        string Version,
        long Created);
}
