// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 米哈游 Wiki 上的绝区零代理人资源：意象影画与技能图标。
/// </summary>
public sealed record ZZZAgentWikiAssets(ImmutableArray<Uri> CinemaImages, ImmutableArray<Uri> SkillIcons);

public interface IZZZCinemaImageService
{
    /// <summary>
    /// 获取代理人的意象影画与技能图标；失败或未收录时返回空数组。
    /// </summary>
    ValueTask<ZZZAgentWikiAssets> GetAgentAssetsAsync(string agentName, CancellationToken token = default);
}

[Service(ServiceLifetime.Singleton, typeof(IZZZCinemaImageService))]
[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class ZZZCinemaImageService : IZZZCinemaImageService
{
    private const string EntryListUrl = "https://act-api-takumi-static.mihoyo.com/common/blackboard/zzz_wiki/v1/home/content/list?app_sn=zzz_wiki&channel_id=43&page_num=1&page_size=100";
    private const string EntryPageUrlFormat = "https://act-api-takumi-static.mihoyo.com/hoyowiki/zzz/wapi/entry_page?app_sn=zzz_wiki&entry_page_id={0}&lang=zh-cn";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private static readonly Regex CinemaTabRegex = new(@"影画展示(\d)", RegexOptions.Compiled);

    private readonly IHttpClientFactory httpClientFactory;
    private readonly ILogger<ZZZCinemaImageService> logger;
    private readonly SemaphoreSlim entryGate = new(1, 1);
    private readonly ConcurrentDictionary<string, ZZZAgentWikiAssets> cache = new(StringComparer.Ordinal);
    private Dictionary<string, int>? entryIds;

    [GeneratedConstructor]
    public partial ZZZCinemaImageService(IServiceProvider serviceProvider);

    public async ValueTask<ZZZAgentWikiAssets> GetAgentAssetsAsync(string agentName, CancellationToken token = default)
    {
        string key = NormalizeName(agentName);
        if (cache.TryGetValue(key, out ZZZAgentWikiAssets cached))
        {
            return cached;
        }

        try
        {
            Dictionary<string, int> ids = await EnsureEntryIdsAsync(token).ConfigureAwait(false);
            if (!TryResolveEntryId(ids, key, out int entryId))
            {
                cache[key] = new([], []);
                return cache[key];
            }

            string url = string.Format(CultureInfo.InvariantCulture, EntryPageUrlFormat, entryId);
            using JsonDocument document = await GetJsonAsync(url, token).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("data", out JsonElement data)
                || !data.TryGetProperty("page", out JsonElement page))
            {
                cache[key] = new([], []);
                return cache[key];
            }

            List<(int Index, string Image)> cinema = [];
            List<(int Index, string Image)> skills = [];
            if (page.TryGetProperty("modules", out JsonElement modules) && modules.ValueKind is JsonValueKind.Array)
            {
                foreach (JsonElement module in modules.EnumerateArray())
                {
                    string moduleId = GetString(module, "id") ?? string.Empty;
                    if (moduleId is not ("279" or "4")
                        || !module.TryGetProperty("components", out JsonElement components)
                        || components.ValueKind is not JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement component in components.EnumerateArray())
                    {
                        string? raw = GetString(component, "data");
                        if (string.IsNullOrEmpty(raw))
                        {
                            continue;
                        }

                        using JsonDocument componentData = JsonDocument.Parse(raw);
                        if (!componentData.RootElement.TryGetProperty("list", out JsonElement list)
                            || list.ValueKind is not JsonValueKind.Array)
                        {
                            continue;
                        }

                        foreach (JsonElement item in list.EnumerateArray())
                        {
                            string tab = GetString(item, "tab_name") ?? string.Empty;
                            string image = GetString(item, "icon") ?? GetString(item, "image") ?? string.Empty;
                            if (string.IsNullOrEmpty(image))
                            {
                                continue;
                            }

                            if (moduleId is "279")
                            {
                                Match match = CinemaTabRegex.Match(tab);
                                if (match.Success && int.TryParse(match.Groups[1].Value, out int tabIndex))
                                {
                                    cinema.Add((tabIndex, image));
                                }
                            }
                            else
                            {
                                int order = GetSkillOrder(tab);
                                if (order >= 0)
                                {
                                    skills.Add((order, image));
                                }
                            }
                        }
                    }
                }
            }

            ZZZAgentWikiAssets result = new(
                [.. cinema.OrderBy(static item => item.Index).Select(static item => item.Image.ToUri()).Distinct()],
                [.. skills.OrderBy(static item => item.Index).Select(static item => item.Image.ToUri()).Distinct()]);
            cache[key] = result;
            return result;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to fetch ZZZ wiki assets for {AgentName}.", agentName);
            cache[key] = new([], []);
            return cache[key];
        }
    }

    private async ValueTask<Dictionary<string, int>> EnsureEntryIdsAsync(CancellationToken token)
    {
        if (entryIds is { } existing)
        {
            return existing;
        }

        await entryGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (entryIds is { } second)
            {
                return second;
            }

            using JsonDocument document = await GetJsonAsync(EntryListUrl, token).ConfigureAwait(false);
            Dictionary<string, int> map = new(StringComparer.Ordinal);
            if (document.RootElement.TryGetProperty("data", out JsonElement data)
                && data.TryGetProperty("list", out JsonElement channels)
                && channels.ValueKind is JsonValueKind.Array)
            {
                JsonElement channel = channels.EnumerateArray().FirstOrDefault();
                if (channel.ValueKind is JsonValueKind.Object
                    && channel.TryGetProperty("list", out JsonElement agents)
                    && agents.ValueKind is JsonValueKind.Array)
                {
                    foreach (JsonElement agent in agents.EnumerateArray())
                    {
                        int entryId = GetInt32(agent, "content_id");
                        if (entryId is 0)
                        {
                            continue;
                        }

                        foreach (string candidate in new[] { GetString(agent, "alias_name"), GetString(agent, "title") })
                        {
                            string normalized = NormalizeName(candidate ?? string.Empty);
                            if (!string.IsNullOrEmpty(normalized))
                            {
                                map.TryAdd(normalized, entryId);
                            }
                        }
                    }
                }
            }

            entryIds = map;
            return map;
        }
        finally
        {
            entryGate.Release();
        }
    }

    private static bool TryResolveEntryId(Dictionary<string, int> map, string normalizedName, out int entryId)
    {
        if (map.TryGetValue(normalizedName, out entryId))
        {
            return true;
        }

        foreach ((string name, int id) in map)
        {
            if (name.Length >= 2 && (normalizedName.Contains(name, StringComparison.Ordinal) || name.Contains(normalizedName, StringComparison.Ordinal)))
            {
                entryId = id;
                return true;
            }
        }

        entryId = 0;
        return false;
    }

    private async ValueTask<JsonDocument> GetJsonAsync(string url, CancellationToken token)
    {
        using HttpClient httpClient = httpClientFactory.CreateClient(nameof(ZZZCinemaImageService));
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("X-Rpc-Wiki_app", "zzz");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(20));
        using HttpResponseMessage response = await httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
    }

    private static int GetSkillOrder(string tabName)
    {
        return tabName switch
        {
            "普攻" => 0,
            "闪避" => 1,
            "支援技" => 2,
            "特殊技" => 3,
            "终结技" or "连携技" => 4,
            "核心技" => 5,
            _ => -1,
        };
    }

    private static string NormalizeName(string value)
    {
        return value
            .Trim()
            .Replace("「", string.Empty, StringComparison.Ordinal)
            .Replace("」", string.Empty, StringComparison.Ordinal)
            .Replace("《", string.Empty, StringComparison.Ordinal)
            .Replace("》", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("・", string.Empty, StringComparison.Ordinal)
            .Replace("·", string.Empty, StringComparison.Ordinal);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null,
        };
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out JsonElement property) && property.TryGetInt32(out int value) ? value : 0;
    }
}
