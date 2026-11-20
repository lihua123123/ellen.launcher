// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Text.Json.Annotation;
using Ellen.Launcher.Core.Text.Json.Converter;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Web.Hoyolab.Hk4e.Event.GachaInfo;

namespace Ellen.Launcher.Model.InterChange.GachaLog;

// ReSharper disable once InconsistentNaming
public sealed class Hk4eItem : IJsonOnDeserialized
{
    // ReSharper disable once InconsistentNaming

    [JsonPropertyName("gacha_type")]
    [JsonEnumHandling(JsonEnumHandling.NumberString)]
    public required GachaType GachaType { get; init; }

    [JsonPropertyName("item_id")]
    [JsonConverter(typeof(ItemIdConverter))]
    public uint ItemId { get; init; }

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
    public string? RankType { get; init; }

    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public required long Id { get; init; }

    [JsonPropertyName("uigf_gacha_type")]
    [JsonEnumHandling(JsonEnumHandling.NumberString)]
    public required UIGFGachaType UIGFGachaType { get; init; }

    public static Hk4eItem From(GachaItem item, string? name, string? itemType, string? rankType, int timezone)
    {
        return new()
        {
            UIGFGachaType = (UIGFGachaType)item.QueryType,
            GachaType = item.GachaType,
            ItemId = item.ItemId,
            Count = "1",
            Time = item.Time.UtcDateTime.AddHours(timezone),
            Name = name,
            ItemType = itemType,
            RankType = rankType,
            Id = item.Id,
        };
    }

    public sealed class ItemIdConverter : JsonConverter<uint>
    {
        public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String when reader.GetString() is "" => 0,
                JsonTokenType.String when uint.TryParse(reader.GetString(), out uint id) => id,
                JsonTokenType.Number when reader.TryGetUInt32(out uint id) => id,
                JsonTokenType.Null => 0,
                _ => throw new JsonException(),
            };
        }

        public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public void OnDeserialized()
    {
        if (!Enum.IsDefined(UIGFGachaType))
        {
            throw new JsonException($"Unsupported UIGFGachaType: {UIGFGachaType}");
        }

        if (!Enum.IsDefined(GachaType))
        {
            throw new JsonException($"Unsupported GachaType: {GachaType}");
        }
    }
}