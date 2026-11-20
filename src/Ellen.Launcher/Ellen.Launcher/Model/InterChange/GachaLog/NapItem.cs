// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Text.Json.Converter;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

namespace Ellen.Launcher.Model.InterChange.GachaLog;

/// <summary>
/// UIGF v4.x 中的单条绝区零调频记录。
/// </summary>
public sealed class NapItem
{
    [JsonPropertyName("gacha_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required int Channel { get; init; }

    // UIGF 只允许标准频段编号，用扩展字段保留重映与回响，供本应用无损回导。
    [JsonPropertyName("ellen_gacha_type")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public int? OriginalChannel { get; init; }

    [JsonIgnore]
    public GachaType GachaType => (Channel, OriginalChannel) switch
    {
        (2, 102) => GachaType.ZZZExclusiveRescreening,
        (3, 103) => GachaType.ZZZWEngineReverberation,
        _ => (GachaType)Channel,
    };

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
    [JsonConverter(typeof(RankTypeConverter))]
    public int? RankType { get; init; }

    public sealed class RankTypeConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString() switch
                {
                    "4" or "S" or "s" => 4,
                    "3" or "A" or "a" => 3,
                    "2" or "B" or "b" => 2,
                    _ => null,
                },
                JsonTokenType.Number when reader.TryGetInt32(out int rank) => rank,
                _ => throw new JsonException(),
            };
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required long Id { get; init; }

    public static NapItem From(GachaItem item, int timezone)
    {
        int channel = item.GachaType switch
        {
            GachaType.ZZZExclusiveRescreening => 2,
            GachaType.ZZZWEngineReverberation => 3,
            _ => (int)item.GachaType,
        };

        return new()
        {
            Channel = channel,
            OriginalChannel = channel == (int)item.GachaType ? null : (int)item.GachaType,
            ItemId = item.ItemId,
            Time = item.Time.ToOffset(TimeSpan.FromHours(timezone)).DateTime,
            Name = string.IsNullOrWhiteSpace(item.Name) ? null : item.Name,
            RankType = (QualityType)item.Quality switch
            {
                QualityType.QUALITY_ORANGE => 4,
                QualityType.QUALITY_PURPLE => 3,
                QualityType.QUALITY_BLUE => 2,
                _ => null,
            },
            Id = item.Id,
        };
    }
}
