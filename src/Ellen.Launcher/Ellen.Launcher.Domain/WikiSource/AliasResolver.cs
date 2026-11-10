// 旧 InternalName → 新 slug 的还原。
// P2 评审修正：解析键是 (slug, stateKey)，stateLabel 只作展示、不参与唯一性判定。
// 深链解析顺序：slug + stateKey → slug 默认状态 → 旧 InternalName 兜底。
// 任何降级都写诊断日志；34 组多对一必须逐组精确还原，不允许一律默认状态。

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ellen.Launcher.Domain.WikiSource;

public sealed class AliasRecord
{
    public string OldId { get; init; } = string.Empty;
    public string OldName { get; init; } = string.Empty;
    public string NewId { get; init; } = string.Empty;
    public string Rule { get; init; } = string.Empty;
    public string MatchedVia { get; init; } = string.Empty;

    /// <summary>稳定的状态解析键（states[].key 或冻结的结构槽位）。空 = 默认状态。</summary>
    public string StateKey { get; init; } = string.Empty;

    /// <summary>命中原文，仅用于 UI 展示与旧数据兼容，绝不参与唯一性判定。</summary>
    public string StateLabel { get; init; } = string.Empty;
}

public sealed class AliasResolution
{
    public string? Slug { get; init; }

    /// <summary>解析键：default / state:&lt;key&gt; / leyline:0 / alias:&lt;index&gt;。</summary>
    public string StateKey { get; init; } = string.Empty;

    /// <summary>仅展示用；可与其它条目重复，不参与深链定位。</summary>
    public string StateLabel { get; init; } = string.Empty;

    public string Strategy { get; init; } = string.Empty;
    public string? FallbackReason { get; init; }
    public bool Degraded => FallbackReason is not null;
}

/// <summary>(slug, stateKey) 复合键校验。aliases 是条目别名，不参与状态唯一性统计。</summary>
public sealed class StateKeyValidation
{
    public bool Unique { get; init; }
    public IReadOnlyList<string> Duplicates { get; init; } = [];

    /// <summary>解析键恒定由 slug + 稳定状态键构成，不使用易变的显示名。</summary>
    public static string FrozenKey(string slug, string stateKey) =>
        (slug ?? string.Empty) + "::" + (stateKey ?? string.Empty);

    public static string Normalize(string value) => new([.. (value ?? string.Empty)
        .Where(c => !char.IsWhiteSpace(c) && c is not ('·' or '・' or '.' or '-' or '—' or '_' or '(' or ')' or '（' or '）'))
        .Select(char.ToLowerInvariant)]);
}

public sealed class AliasResolver
{
    private readonly IReadOnlyDictionary<string, AliasRecord> byOldId;
    private readonly WikiDiagnosticsLog log;

    /// <summary>冻结表里登记过的 slug 集合（含中文 slug）；用于判定深链参数是不是新形态 id。</summary>
    public IReadOnlySet<string> KnownSlugs { get; }

    /// <summary>冻结的 (slug, stateKey) 复合键集合。</summary>
    public IReadOnlySet<string> FrozenKeys { get; }

    public AliasResolver(IEnumerable<AliasRecord> records, WikiDiagnosticsLog? log = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        this.log = log ?? new WikiDiagnosticsLog();
        Dictionary<string, AliasRecord> map = new(StringComparer.Ordinal);
        HashSet<string> slugs = new(StringComparer.Ordinal);
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (AliasRecord record in records)
        {
            map[record.OldId] = record;
            if (!string.IsNullOrEmpty(record.NewId))
            {
                slugs.Add(record.NewId);
                keys.Add(StateKeyValidation.FrozenKey(record.NewId, string.IsNullOrEmpty(record.StateKey) ? "default" : record.StateKey));
            }
        }

        byOldId = map;
        KnownSlugs = slugs;
        FrozenKeys = keys;
    }

    /// <summary>旧 InternalName → (slug, stateKey)。</summary>
    public AliasResolution Resolve(string oldId)
    {
        if (!byOldId.TryGetValue(oldId, out AliasRecord? record) || string.IsNullOrEmpty(record.NewId))
        {
            this.log.Add("alias-migration", WikiSourceStamp.FromBundled("alias 未命中"), $"{oldId}: 无 alias，回退旧 InternalName", "alias 未命中");
            return new AliasResolution
            {
                Slug = oldId,
                Strategy = "legacy-internal-name",
                FallbackReason = "alias 未命中，落到旧 InternalName 兜底",
            };
        }

        if (!string.IsNullOrEmpty(record.StateKey) && !string.Equals(record.StateKey, "default", StringComparison.Ordinal))
        {
            return new AliasResolution
            {
                Slug = record.NewId,
                StateKey = record.StateKey,
                StateLabel = record.StateLabel,
                Strategy = "slug+stateKey",
            };
        }

        return new AliasResolution
        {
            Slug = record.NewId,
            StateKey = "default",
            StateLabel = record.StateLabel,
            Strategy = "slug",
        };
    }

    /// <summary>深链解析：slug + stateKey → slug 默认 → 旧 InternalName。</summary>
    public AliasResolution ResolveDeepLink(string slug, string? stateKey)
    {
        if (string.IsNullOrEmpty(stateKey) || string.Equals(stateKey, "default", StringComparison.Ordinal))
        {
            return new AliasResolution { Slug = slug, StateKey = "default", Strategy = "slug" };
        }

        // stateKey 必须在冻结的 (slug, stateKey) 空间内；无法判定时显式降级，不猜。
        bool known = FrozenKeys.Contains(StateKeyValidation.FrozenKey(slug, stateKey));
        if (!known)
        {
            this.log.Add("alias-migration", WikiSourceStamp.FromBundled("stateKey 不在冻结表内"),
                $"{slug}::{stateKey} 未登记，降级到 slug 默认状态", "stateKey 未冻结");
            return new AliasResolution
            {
                Slug = slug,
                StateKey = "default",
                Strategy = "slug-default",
                FallbackReason = "stateKey 不在冻结表内，降级到默认状态",
            };
        }

        return new AliasResolution { Slug = slug, StateKey = stateKey, Strategy = "slug+stateKey" };
    }

    /// <summary>P2 冻结用：校验 (slug, stateKey) 全表唯一。aliases 不得混入。</summary>
    public static StateKeyValidation ValidateStateKeys(IEnumerable<(string Slug, string StateKey)> keys)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<string> duplicates = [];
        foreach ((string slug, string stateKey) in keys)
        {
            string key = StateKeyValidation.FrozenKey(slug ?? string.Empty, stateKey ?? string.Empty);
            if (!seen.Add(key))
            {
                duplicates.Add(key);
            }
        }

        return new StateKeyValidation { Unique = duplicates.Count == 0, Duplicates = duplicates };
    }
}
