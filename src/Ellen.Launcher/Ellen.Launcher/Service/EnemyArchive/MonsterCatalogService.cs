// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.EnemyArchive;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Monster;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;

namespace Ellen.Launcher.Service.EnemyArchive;

/// <summary>
/// 从应用目录的 <c>MonsterCatalog.json</c> 懒加载图鉴数据。
/// <para>
/// 数据由 <c>workspace/ec_tmp/etl/build_catalog.py</c> 从上游 GIEnemyChartXML 生成：
/// 722 条 = 722 个 <c>InternalName</c>，每条自带名字 / 分类 / 头像 / 基础值 / 完整档案。
/// 资源缺失或格式错误时只记日志并返回空集合，不抛异常。
/// </para>
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IMonsterCatalogService))]
public sealed partial class MonsterCatalogService : IMonsterCatalogService
{
    private const string ResourceRelativePath = "Resource/EnemyArchives/MonsterCatalog.json";

    private readonly ILogger<MonsterCatalogService> logger;

    private ImmutableArray<MonsterCatalogEntry>? entries;

    [GeneratedConstructor]
    public partial MonsterCatalogService(IServiceProvider serviceProvider);

    public ValueTask<ImmutableArray<MonsterCatalogEntry>> GetEntriesAsync()
    {
        return entries is { } cached ? ValueTask.FromResult(cached) : LoadAsync();
    }

    private async ValueTask<ImmutableArray<MonsterCatalogEntry>> LoadAsync()
    {
        string path = InstalledLocation.GetAbsolutePath(ResourceRelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Monster catalog not found: {Path}", path);
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            MonsterCatalogFile? file = await JsonSerializer
                .DeserializeAsync<MonsterCatalogFile>(stream)
                .ConfigureAwait(false);

            MonsterCatalogEntry[] raw = file?.Entries ?? [];
            ImmutableArray<MonsterCatalogEntry> parsed = raw.Length is 0
                ? []
                : [.. raw.Select(static entry => WithBaseValue(entry))];

            entries = parsed;
            return parsed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load monster catalog from {Path}", path);
            return [];
        }
    }

    /// <summary>
    /// 把 JSON 里的裸数值装配成详情页滑条要的 <see cref="MonsterBaseValue"/> + 成长曲线。
    /// <para>
    /// 曲线名来自上游（<c>HP_1</c> / <c>HP_2</c> / <c>ATK_1</c> / <c>ATK_2</c> / <c>DEF_1</c>）；
    /// <c>HP_0</c> / <c>HP_3</c> 这两种在客户端曲线表里没有对应条目，回落到 <c>HP_1</c>。
    /// </para>
    /// </summary>
    private static MonsterCatalogEntry WithBaseValue(MonsterCatalogEntry entry)
    {
        EnemyArchiveResistanceValues? values = entry.Archive?.Resistances?.Values;

        return new MonsterCatalogEntry
        {
            Id = entry.Id,
            Name = entry.Name,
            Subtitle = entry.Subtitle,
            DisplayName = entry.DisplayName,
            Campaign = entry.Campaign,
            Category = entry.Category,
            CategoryName = entry.CategoryName,
            Tier = entry.Tier,
            TierName = entry.TierName,
            Icon = entry.Icon,
            BaseHp = entry.BaseHp,
            HpCurve = entry.HpCurve,
            BaseAtk = entry.BaseAtk,
            AtkCurve = entry.AtkCurve,
            BaseDef = entry.BaseDef,
            DefCurve = entry.DefCurve,
            Archive = entry.Archive,
            BaseValue = new MonsterBaseValue
            {
                HpBase = (float)entry.BaseHp,
                AttackBase = (float)entry.BaseAtk,
                DefenseBase = (float)entry.BaseDef,
                FireSubHurt = ParseResistance(values?.Pyro),
                WaterSubHurt = ParseResistance(values?.Hydro),
                GrassSubHurt = ParseResistance(values?.Dendro),
                ElecSubHurt = ParseResistance(values?.Electro),
                WindSubHurt = ParseResistance(values?.Anemo),
                IceSubHurt = ParseResistance(values?.Cryo),
                RockSubHurt = ParseResistance(values?.Geo),
                PhysicalSubHurt = ParseResistance(values?.Physical),
            },
            GrowCurves = new TypeValueCollection<FightProperty, GrowCurveType>(
            [
                new(FightProperty.FIGHT_PROP_BASE_HP, ToCurve(entry.HpCurve, GrowCurveType.GROW_CURVE_HP)),
                new(FightProperty.FIGHT_PROP_BASE_ATTACK, ToCurve(entry.AtkCurve, GrowCurveType.GROW_CURVE_ATTACK)),
                new(FightProperty.FIGHT_PROP_BASE_DEFENSE, GrowCurveType.GROW_CURVE_DEFENSE),
            ]),
        };
    }

    private static GrowCurveType ToCurve(string? key, GrowCurveType fallback)
    {
        return key switch
        {
            "HP_1" => GrowCurveType.GROW_CURVE_HP,
            "HP_2" => GrowCurveType.GROW_CURVE_HP_2,
            "ATK_1" => GrowCurveType.GROW_CURVE_ATTACK,
            "ATK_2" => GrowCurveType.GROW_CURVE_ATTACK_2,
            "DEF_1" => GrowCurveType.GROW_CURVE_DEFENSE,
            _ => fallback,
        };
    }

    private static float ParseResistance(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        string value = text.Trim();
        if (value.Contains("IMM", StringComparison.OrdinalIgnoreCase) || value.Contains('免'))
        {
            return 100;
        }

        return float.TryParse(value.TrimEnd('%', ' '), NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
            ? number
            : 0;
    }

    private sealed class MonsterCatalogFile
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("entries")]
        public MonsterCatalogEntry[] Entries { get; set; } = [];
    }
}
