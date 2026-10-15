// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Text.Json.Annotation;
using Ellen.Launcher.Core.Text.Json.Converter;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

namespace Ellen.Launcher.Model.InterChange.GachaLog;

/// <summary>
/// 绝区零调频记录在 UIGF v4.2 的 <c>nap</c> 条目中的单条记录。
/// 与原神不同，<c>gacha_type</c> 直接对应 <see cref="GachaType"/> 的绝区零取值
/// （1/2/3/5/102/103），无需 <c>uigf_gacha_type</c> 映射；<c>rank_type</c> 为绝区零稀有度（2=B/3=A/4=S）。
/// </summary>
public sealed class NapItem
{
    [JsonPropertyName("gacha_type")]
    [JsonEnumHandling(JsonEnumHandling.NumberString)]
    public required GachaType GachaType { get; init; }

    [JsonPropertyName("item_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required uint ItemId { get; init; }

    [JsonPropertyName("count")]
    public string Count { get; init; } = "1";

    [JsonPropertyName("time")]
    [JsonConverter(typeof(SimpleDateTimeConverter))]
    public required DateTime Time { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("item_type")]
    public string? ItemType { get; init; }

    [JsonPropertyName("rank_type")]
    [JsonEnumHandling(JsonEnumHandling.NumberString)]
    public required QualityType RankType { get; init; }

    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required long Id { get; init; }
}
