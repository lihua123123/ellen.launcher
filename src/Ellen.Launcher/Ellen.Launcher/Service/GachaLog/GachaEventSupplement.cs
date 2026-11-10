// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Service.Metadata;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.GachaLog;

/// <summary>
/// 卡池事件（<c>GachaEvent.json</c>）的运行时补充层。
/// 随包快照与上游仓库都是只读的，新版本卡池由 <see cref="GachaEventAutoUpdater"/> 抓到
/// <c>Cache\GachaEvent.extra.json</c>；读取时与基础数据合并，合并规则：
/// <list type="bullet">
/// <item>近 <see cref="RecentWindowDays"/> 天内的条目以补充数据为准，可纠正起止时间与 UP 列表；</item>
/// <item>更早的条目只做缺失补充，避免上游历史数据回退影响既有统计。</item>
/// </list>
/// 合并结果按 <see cref="GachaEvent.From"/> 升序，保证祈愿记录按时间二分匹配的正确性。
/// </summary>
internal static class GachaEventSupplement
{
    private const int RecentWindowDays = 180;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string OverrideFilePath { get; } = Path.Combine(EllenRuntime.LocalCacheDirectory, "GachaEvent.extra.json");

    public static async ValueTask<ImmutableArray<GachaEvent>> GetMergedAsync(IMetadataService metadataService, CancellationToken token)
    {
        ImmutableArray<GachaEvent> baseEvents = await metadataService
            .FromCacheOrFileAsync<GachaEvent>(MetadataFileStrategies.GachaEvent, token)
            .ConfigureAwait(false);

        ImmutableArray<GachaEvent> overrideEvents = await ReadOverrideAsync(token).ConfigureAwait(false);
        return overrideEvents.IsDefaultOrEmpty ? baseEvents : Merge(baseEvents, overrideEvents);
    }

    public static async ValueTask<ImmutableArray<GachaEvent>> ReadOverrideAsync(CancellationToken token)
    {
        string path = OverrideFilePath;
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
            List<GachaEvent>? events = JsonSerializer.Deserialize<List<GachaEvent>>(json, Options);
            if (events is null)
            {
                return [];
            }

            return [.. events];
        }
        catch (Exception)
        {
            // 缓存文件损坏时退回基础数据，不要让整个祈愿页打不开。
            return [];
        }
    }

    public static async ValueTask WriteOverrideAsync(IReadOnlyCollection<GachaEvent> events, CancellationToken token)
    {
        string path = OverrideFilePath;
        string? folder = Path.GetDirectoryName(path);
        if (folder is not null)
        {
            Directory.CreateDirectory(folder);
        }

        string json = JsonSerializer.Serialize(events, Options);
        string tempPath = $"{path}.tmp";
        await File.WriteAllTextAsync(tempPath, json, token).ConfigureAwait(false);
        File.Move(tempPath, path, true);
    }

    private static ImmutableArray<GachaEvent> Merge(ImmutableArray<GachaEvent> baseEvents, ImmutableArray<GachaEvent> overrideEvents)
    {
        DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-RecentWindowDays);
        Dictionary<(string Version, uint Order, GachaType Type), GachaEvent> map = new(baseEvents.Length + overrideEvents.Length);

        foreach (GachaEvent gachaEvent in baseEvents)
        {
            map[(gachaEvent.Version, gachaEvent.Order, gachaEvent.Type)] = gachaEvent;
        }

        foreach (GachaEvent gachaEvent in overrideEvents)
        {
            (string, uint, GachaType) key = (gachaEvent.Version, gachaEvent.Order, gachaEvent.Type);
            if (gachaEvent.To >= cutoff || !map.ContainsKey(key))
            {
                map[key] = gachaEvent;
            }
        }

        return [.. map.Values.OrderBy(static gachaEvent => gachaEvent.From)];
    }
}
