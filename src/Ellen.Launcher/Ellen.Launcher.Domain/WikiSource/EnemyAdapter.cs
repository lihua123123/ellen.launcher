// C2 DTO → 领域模型。P1 唯一允许做字段转换的地方；ViewModel/XAML 不碰 DTO。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Ellen.Launcher.Domain.WikiSource.Contracts;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class EnemyAdapter
{
    private const string ImageBase = "https://ellen.wiki/images/enemies";

    private static readonly Dictionary<string, string> TierNames = new(StringComparer.Ordinal)
    {
        ["Common"] = "普通",
        ["Elite"] = "精英",
        ["Boss"] = "首领",
        ["Legend"] = "传说",
        ["Animal"] = "动物",
        ["Leyline"] = "幽境危战",
        ["Weekly"] = "周本",
        ["None"] = string.Empty,
    };

    private static readonly (string Key, string Label)[] ElementOrder =
    [
        ("pyro", "火"),
        ("hydro", "水"),
        ("anemo", "风"),
        ("electro", "雷"),
        ("dendro", "草"),
        ("cryo", "冰"),
        ("geo", "岩"),
        ("physical", "物"),
    ];

    private readonly WikiDiagnosticsLog log;
    private readonly bool strict;

    public EnemyAdapter(WikiDiagnosticsLog? log = null, bool strict = false)
    {
        this.log = log ?? new WikiDiagnosticsLog();
        this.strict = strict;
    }

    public WikiEnemy Adapt(EnemyItemDto item, EnemyIndexEntryDto? index = null, WikiSourceStamp? stamp = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        WikiSourceStamp source = stamp ?? WikiSourceStamp.FromEllenWiki("C2 /api/enemies", null, null);
        string id = item.Id;

        WikiBaseValue? baseValue = BuildBaseValue(item, source);
        IReadOnlyList<WikiResistanceState> resistances = BuildResistances(item);
        List<WikiImageRef> images = BuildImages(item, source);

        return new WikiEnemy
        {
            Id = id,
            Name = item.Name,
            NameEn = item.NameEn,
            Subtitle = item.Subtitle,
            Description = string.IsNullOrWhiteSpace(item.Subtitle) ? null : item.Subtitle,
            CategoryKey = item.Category,
            CategoryName = string.IsNullOrWhiteSpace(item.CategoryLabel) ? (index?.CategoryLabel ?? string.Empty) : item.CategoryLabel,
            Tier = item.Tier,
            TierName = item.Tier is { } tier && TierNames.TryGetValue(tier, out string? name) ? name : null,
            DefaultLevel = ToInt(item.DefaultLevel),
            BaseValue = baseValue,
            IconFile = string.IsNullOrWhiteSpace(item.Icon) ? null : item.Icon,
            ResistanceStates = resistances,
            Skills = item.Skills.Select(skill => AdaptSkill(skill, id)).ToList(),
            Notes = item.Notes.Select(note => new WikiNote { Text = note.Text, Description = WikiRichText.Parse(note.Desc) }).ToList(),
            Shields = item.Shields.Select(shield => new WikiShield
            {
                Name = shield.Name,
                Description = WikiRichText.Parse(shield.Description),
                ElementType = shield.ElementType,
                Duration = NormalizeDuration(shield.Duration),
            }).ToList(),
            ChargeBars = item.ChargeBar.Select(bar => new WikiChargeBar { Name = bar.Name, Description = WikiRichText.Parse(bar.Description) }).ToList(),
            Energy = item.Energy is null ? null : new WikiEnergy
            {
                Element = item.Energy.Element,
                Raw = item.Energy.Raw,
                Rows = item.Energy.Rows.Select(row => new WikiEnergyRow { Hp = row.Hp, Particle = row.Particle, Orb = row.Orb }).ToList(),
            },
            LevelRows = item.LevelRows.Select(row => new WikiLevelRow
            {
                Label = row.Label,
                Level = row.Level,
                Hp = row.Hp,
                Atk = row.Atk,
                Weight = row.Weight,
                Tenacity = row.Tenacity,
            }).ToList(),
            Images = images,
            Aliases = item.Aliases,
            Tags = item.Tags,
            Leyline = item.Leyline is null ? null : AdaptLeyline(item.Leyline, id),
            States = (item.States ?? new List<EnemyStateDto>()).Select(state => new WikiEnemyState { Key = state.Key, Label = state.Label, Name = state.Name }).ToList(),
            DefaultState = item.DefaultState,
            Stamp = source,

            // P4 扩展槽位：当前源里没有这些字段，保持 null，不编造、不删槽位。
            Evidence = item.Evidence,
            Mechanics = item.Mechanics?.Select(m => new WikiMechanic { Title = m.Title, Text = WikiRichText.Parse(m.Text) }).ToList(),
            Variants = item.Variants?.Select(v => new WikiVariant { Name = v.Name, Level = v.Level, Hp = v.Hp, Atk = v.Atk }).ToList(),
            Campaign = item.Campaign,
            DisplayName = item.DisplayName,
        };
    }

    private WikiBaseValue? BuildBaseValue(EnemyItemDto item, WikiSourceStamp source)
    {
        EnemyStatDto? hp = item.BaseStats?.Hp;
        EnemyStatDto? atk = item.BaseStats?.Atk;
        EnemyStatDto? def = item.BaseStats?.Def;

        // 40 条上游就是 {}：记「未知」，绝不填 0，也不回退内置元数据。
        if (hp?.Value is null && atk?.Value is null && def?.Value is null)
        {
            if (strict)
            {
                log.Add("base-stats-unknown", source, $"{item.Id}: baseStats 为空对象，BaseValue 置 null（未知）");
            }

            return null;
        }

        // 曲线权威源是 launcher zip 内的 MonsterCurve；P1 只保留键，不静默回退内置。
        log.Add("curve-source", source, $"{item.Id}: 曲线键 hp={hp?.Curve ?? "-"} atk={atk?.Curve ?? "-"} def={def?.Curve ?? "-"}；倍率须来自 zip 内 MonsterCurve(Type 12 = DEF_1)，当前 zip 缺表", CurveContract.ValidateMissingReason);

        return new WikiBaseValue
        {
            Hp = hp?.Value ?? 0,
            Atk = atk?.Value ?? 0,
            Def = def?.Value ?? 0,
            HpCurve = hp?.Curve,
            AtkCurve = atk?.Curve,
            DefCurve = def?.Curve,
        };
    }

    private static IReadOnlyList<WikiResistanceState> BuildResistances(EnemyItemDto item)
    {
        if (item.Resistances.Count == 0)
        {
            return [];
        }

        int defaultIndex = SelectDefaultIndex(item);
        List<WikiResistanceState> result = [];
        for (int i = 0; i < item.Resistances.Count; i++)
        {
            EnemyResistanceStateDto state = item.Resistances[i];
            result.Add(new WikiResistanceState
            {
                Name = state.Name,
                IsDefault = i == defaultIndex,
                DiffNote = state.DiffNote,
                Evidence = state.Evidence,
                Condition = state.Condition,
                Cells = ElementOrder.Select(pair => BuildCell(pair, GetValue(state, pair.Key))).ToList(),
            });
        }

        return result;
    }

    private static int SelectDefaultIndex(EnemyItemDto item)
    {
        for (int i = 0; i < item.Resistances.Count; i++)
        {
            if (item.Resistances[i].Name.Contains("正常", StringComparison.Ordinal))
            {
                return i;
            }
        }

        if (!string.IsNullOrEmpty(item.DefaultState))
        {
            EnemyStateDto? state = (item.States ?? new List<EnemyStateDto>()).FirstOrDefault(s => string.Equals(s.Key, item.DefaultState, StringComparison.Ordinal));
            int index = item.Resistances.FindIndex(r => string.Equals(r.Name, state?.Name, StringComparison.Ordinal));
            if (index >= 0)
            {
                return index;
            }
        }

        return 0;
    }

    private static JsonElement? GetValue(EnemyResistanceStateDto state, string key)
    {
        return key switch
        {
            "pyro" => state.Pyro,
            "hydro" => state.Hydro,
            "anemo" => state.Anemo,
            "electro" => state.Electro,
            "dendro" => state.Dendro,
            "cryo" => state.Cryo,
            "geo" => state.Geo,
            "physical" => state.Physical,
            _ => null,
        };
    }

    private static WikiResistanceCell BuildCell((string Key, string Label) pair, JsonElement? element)
    {
        string text = string.Empty;
        bool immune = false;
        bool high = false;

        if (element.HasValue)
        {
            JsonElement value = element.Value;
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    double number = value.GetDouble();
                    text = number.ToString(CultureInfo.InvariantCulture) + "%";
                    high = number >= 60;
                    break;
                case JsonValueKind.String:
                    string raw = value.GetString() ?? string.Empty;
                    if (raw.Contains("IMM", StringComparison.OrdinalIgnoreCase) || raw.Contains('免'))
                    {
                        immune = true;
                        text = "IMM";
                    }
                    else if (raw != "—" && raw.Length > 0)
                    {
                        text = raw;
                    }

                    break;
                default:
                    break;
            }
        }

        return new WikiResistanceCell
        {
            Key = pair.Key,
            Label = pair.Key == "physical" ? "物理抗性" : pair.Label + "元素抗性",
            ValueText = text,
            IsImmune = immune,
            IsHigh = high,
        };
    }

    private WikiSkill AdaptSkill(EnemySkillDto skill, string enemyId)
    {
        return new WikiSkill
        {
            Name = skill.Name ?? string.Empty,
            Tag = skill.Type,
            Description = WikiRichText.Parse(skill.Description),
            Icon = string.IsNullOrWhiteSpace(skill.Icon) ? null : ImageRef(enemyId, skill.Icon),
            Tips = skill.Tips,
            Combo = skill.Combo,
            Damage = skill.Damage,
            Dodge = skill.Dodge,
        };
    }

    private List<WikiImageRef> BuildImages(EnemyItemDto item, WikiSourceStamp source)
    {
        List<string> files = [];
        if (!string.IsNullOrWhiteSpace(item.Icon))
        {
            files.Add(item.Icon);
        }

        files.AddRange(item.Images);
        files.AddRange(item.LeylineImages);
        files.AddRange(item.Skills.Where(s => !string.IsNullOrWhiteSpace(s.Icon)).Select(s => s.Icon!));
        if (!string.IsNullOrWhiteSpace(item.Leyline?.Portrait))
        {
            files.Add(item.Leyline.Portrait);
        }

        List<WikiImageRef> distinct = [.. files.Distinct(StringComparer.Ordinal).Select(file => ImageRef(item.Id, file))];
        if (distinct.Count > 0)
        {
            log.Add("image-hash-missing", source, $"{item.Id}: {distinct.Count} 个图片引用无内容 hash，P1 按 id/file 缓存，P4 由 assets manifest 补 sha256；失败降级占位图");
        }

        return distinct;
    }

    private static WikiImageRef ImageRef(string enemyId, string file)
    {
        return new WikiImageRef
        {
            EnemyId = enemyId,
            File = file,
            Uri = $"{ImageBase}/{enemyId}/{file}",
            CacheKey = $"{enemyId}/{file}",
            HashAvailable = false,
            ContentHash = null,
        };
    }

    private static WikiLeyline AdaptLeyline(EnemyLeylineDto leyline, string enemyId)
    {
        return new WikiLeyline
        {
            Name = leyline.Name,
            CategoryLabel = leyline.CategoryLabel,
            Portrait = string.IsNullOrWhiteSpace(leyline.Portrait) ? null : ImageRef(enemyId, leyline.Portrait),
        };
    }

    /// <summary>shields[].duration 上游混合 string/number，统一成展示字符串；空值留空，不填 0。</summary>
    private static string? NormalizeDuration(JsonElement? element)
    {
        if (!element.HasValue)
        {
            return null;
        }

        JsonElement value = element.Value;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetDouble().ToString(CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    private static int? ToInt(JsonElement? element)
    {
        double? value = ToDouble(element);
        return value.HasValue ? (int)value.Value : null;
    }

    private static double? ToDouble(JsonElement? element)
    {
        if (!element.HasValue)
        {
            return null;
        }

        JsonElement value = element.Value;
        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : null,
            _ => null,
        };
    }
}
