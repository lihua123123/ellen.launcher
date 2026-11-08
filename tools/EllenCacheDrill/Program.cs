using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ellen.Launcher.Domain.WikiSource;

namespace EllenCacheDrill;

/// <summary>
/// G6 缓存演练（W1-7b）：构造一个超过全局上限的缓存目录，调用客户端真实的
/// <see cref="WikiCacheStore.Prune(WikiCachePruneOptions)"/>，把 <see cref="WikiCachePruneResult"/>
/// 原样落盘。判定不在这里做二次计算——分层字节来自客户端返回值。
///
/// 两个场景各跑一遍：
///   tier-quota  —— 用 WikiCacheTiers.Defaults(limit)，验证「层内淘汰、互不挤占」
///   global-lru  —— images 层不设独立限额，验证「全局 LRU 按 EvictionPriority 跨层淘汰，thumbs 不被牵连」
///
/// 退出码：0 成功；2 参数/环境/构造错误；3 断言失败（不写出正式报告）。
///
/// isolationOk 口径：**items 与 thumbs 两层字节前后不变**（infra 单列 InfraIntact）。
/// 只比 thumbs 会漏掉「削 images 却意外带走 items」这种跨层挤占。
/// </summary>
internal static class Program
{
    private const string Schema = "ellen-rollout-probe-cache-v1";
    private const string VerifiedTarget = "client-prune";

    private static int Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"[drill] 参数错误：{ex.Message}");
            return 2;
        }

        if (options.ShowHelp)
        {
            Options.PrintUsage();
            return 0;
        }

        try
        {
            return Run(options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[drill] 环境错误：{ex.Message}");
            return 2;
        }
    }

    private static int Run(Options options)
    {
        // 安全闸：只允许空目录或不存在，绝不清理已有内容。
        if (Directory.Exists(options.Root) && Directory.EnumerateFileSystemEntries(options.Root).Any())
        {
            Console.Error.WriteLine($"[drill] root 非空，拒绝运行：{options.Root}");
            Console.Error.WriteLine("        请指定一个空目录；本工具不会删除任何已有文件。");
            return 2;
        }

        Directory.CreateDirectory(options.Root);

        List<string> failures = [];
        List<ScenarioReport> scenarios = [];

        foreach (string name in options.Scenarios)
        {
            ScenarioReport report = RunScenario(options, name, failures);
            scenarios.Add(report);
            Console.WriteLine($"[drill] {name}: before={report.BytesBefore} after={report.BytesAfter} "
                + $"tier={report.RemovedByTier} lru={report.RemovedByLru} unreferenced={report.RemovedUnreferenced} "
                + $"isolation={report.IsolationOk}");
        }

        bool pruneBoundaryOk = failures.Count == 0;
        if (!pruneBoundaryOk)
        {
            Console.Error.WriteLine("[drill] 断言失败，不写正式报告：");
            foreach (string f in failures)
            {
                Console.Error.WriteLine($"  - {f}");
            }

            return 3;
        }

        Payload payload = new()
        {
            Schema = Schema,
            GeneratedAt = DateTimeOffset.Now.ToString("O"),
            VerifiedTarget = VerifiedTarget,
            Provenance = "constructed",
            RealTraffic = false,
            Note = "缓存内容由本工具本地写入构造（非下载）。验证对象是 WikiCacheStore.Prune 的真实实现，"
                 + "不代表真实 1.35 GiB 流量演练；对外只能表述为「Prune 逻辑已验证」。",
            LimitBytes = options.LimitBytes,
            TargetBytes = options.TargetBytes,
            FileBytes = options.FileBytes,
            Scenarios = scenarios,
            PruneBoundaryOk = true,
            IsolationOk = scenarios.All(s => s.IsolationOk),
        };

        string outPath = Path.GetFullPath(options.Out);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        File.WriteAllText(outPath, JsonSerializer.Serialize(payload, JsonContext.Default.Payload));
        Console.WriteLine($"[drill] 已写出 {outPath}");
        return 0;
    }

    private static ScenarioReport RunScenario(Options options, string scenario, List<string> failures)
    {
        string root = Path.Combine(options.Root, scenario);
        if (Directory.Exists(root))
        {
            // 同一 root 下重跑会读到旧索引与旧文件，直接拒绝，避免把上一轮结果混进来。
            throw new IOException($"场景目录已存在，请换一个 root：{root}");
        }

        Directory.CreateDirectory(root);

        IReadOnlyList<WikiCacheTierLimit> tiers = BuildTiers(options.LimitBytes, scenario);
        (int items, int thumbs, int images) = Plan(options);

        using WikiCacheStore store = new(root);

        // infra：两个受保护文件，必须保住。
        Write(store, "manifest.json", 4096);
        Write(store, "enemy-index.json", 700 * 1024);

        for (int i = 0; i < items; i++)
        {
            Write(store, $"enemy:{i:D4}.json", (int)options.FileBytes);
        }

        for (int i = 0; i < thumbs; i++)
        {
            Write(store, $"thumb:{i:D4}.webp", (int)options.FileBytes);
        }

        for (int i = 0; i < images; i++)
        {
            Write(store, $"image:{i:D4}.webp", (int)options.FileBytes);
        }

        List<WikiCacheTierUsage> before = [.. store.GetTierUsage(tiers)];
        long bytesBefore = before.Sum(t => t.Bytes);

        // KeepKeys 模拟「items 仍被数据集引用」；image/thumb 走 KeepPrefixes，
        // 否则它们会在第一步被当 unreferenced 删光，走不到 LRU/分层淘汰。
        List<string> keepKeys = [.. Enumerable.Range(0, items).Select(i => $"enemy:{i:D4}.json")];

        WikiCachePruneResult result = store.Prune(new WikiCachePruneOptions
        {
            KeepKeys = keepKeys,
            KeepPrefixes = ["image:", "thumb:", "manifest.json", "enemy-index.json"],
            MaxBytes = options.LimitBytes,
            TierLimits = tiers,
        });

        List<WikiCacheTierUsage> after = [.. result.Tiers];
        long infraAfter = BytesOf(after, WikiCacheTiers.Infra);
        long thumbsAfter = BytesOf(after, WikiCacheTiers.Thumbnails);
        long itemsAfter = BytesOf(after, WikiCacheTiers.Items);
        long infraBefore = BytesOf(before, WikiCacheTiers.Infra);
        long thumbsBefore = BytesOf(before, WikiCacheTiers.Thumbnails);
        long itemsBefore = BytesOf(before, WikiCacheTiers.Items);

        // 层间隔离必须两层都比：只比 thumbs 时，「images 被削却意外带走 items」会漏检
        // （tier-quota 场景 items 远低于层限额，本该一个都不掉）。
        bool itemsOk = itemsAfter == itemsBefore;
        bool thumbsOk = thumbsAfter == thumbsBefore;
        bool isolationOk = itemsOk && thumbsOk;
        bool infraOk = infraAfter == infraBefore;

        // 断言：构造正确性优先于「结果好看」。
        if (result.RemovedUnreferenced != 0)
        {
            failures.Add($"{scenario}: removedUnreferenced={result.RemovedUnreferenced}（应 0；"
                + "非 0 说明 image/thumb 没命中 KeepPrefixes，验证对象已跑偏）");
        }

        if (!infraOk)
        {
            failures.Add($"{scenario}: infra 层被淘汰（{infraBefore} → {infraAfter}），受保护层不应变动");
        }

        if (!itemsOk)
        {
            failures.Add($"{scenario}: items 被动过（{itemsBefore} → {itemsAfter}），"
                + "items 在层限额内且优先级低于 images，不应被牵连");
        }

        if (!thumbsOk)
        {
            failures.Add($"{scenario}: thumbs 被动过（{thumbsBefore} → {thumbsAfter}），层间隔离失败");
        }

        if (result.BytesAfter > options.LimitBytes)
        {
            failures.Add($"{scenario}: bytesAfter={result.BytesAfter} 仍超过上限 {options.LimitBytes}");
        }

        if (scenario == "tier-quota" && result.RemovedByTier <= 0)
        {
            failures.Add($"{scenario}: removedByTier=0，images 超过层限额却没有触发层内淘汰（TierLimits 未生效？）");
        }

        if (scenario == "global-lru" && result.RemovedByLru <= 0)
        {
            failures.Add($"{scenario}: removedByLru=0，超出全局上限却没有触发 LRU");
        }

        return new ScenarioReport
        {
            Scenario = scenario,
            LimitBytes = options.LimitBytes,
            BytesBefore = result.BytesBefore,
            BytesAfter = result.BytesAfter,
            RemainingFiles = result.RemainingFiles,
            RemovedUnreferenced = result.RemovedUnreferenced,
            RemovedByTier = result.RemovedByTier,
            RemovedByLru = result.RemovedByLru,
            TiersBefore = [.. before.Select(ToTier)],
            TiersAfter = [.. after.Select(ToTier)],
            IsolationOk = isolationOk,
            ItemsIntact = itemsOk,
            ThumbsIntact = thumbsOk,
            InfraIntact = infraOk,
        };
    }

    private static IReadOnlyList<WikiCacheTierLimit> BuildTiers(long limitBytes, string scenario)
    {
        IReadOnlyList<WikiCacheTierLimit> defaults = WikiCacheTiers.Defaults(limitBytes);
        if (scenario != "global-lru")
        {
            return defaults;
        }

        // global-lru：把 images 的独立限额关掉（MaxBytes=0 → Prune 跳过 tier 淘汰），
        // 保留 EvictionPriority=30，让全局 LRU 按「images 先淘汰」工作。
        return
        [
            .. defaults.Select(t => string.Equals(t.Name, WikiCacheTiers.Images, StringComparison.Ordinal)
                ? new WikiCacheTierLimit
                {
                    Name = t.Name,
                    Prefixes = t.Prefixes,
                    MaxBytes = 0,
                    Protected = t.Protected,
                    EvictionPriority = t.EvictionPriority,
                }
                : t),
        ];
    }

    /// <summary>按目标字节规划各层文件数：items/thumbs 留在各自限额内，images 吃掉剩余。</summary>
    private static (int Items, int Thumbs, int Images) Plan(Options options)
    {
        long file = options.FileBytes;
        long itemsBudget = (long)(options.TargetBytes * 0.085);   // ≈128 MiB @1.5 GiB，低于 items 20% 限额
        long thumbsBudget = (long)(options.TargetBytes * 0.042);  // ≈64 MiB，低于 thumbs 18% 限额
        long imagesBudget = options.TargetBytes - itemsBudget - thumbsBudget - (700 * 1024) - 4096;

        int items = (int)Math.Max(1, itemsBudget / file);
        int thumbs = (int)Math.Max(1, thumbsBudget / file);
        int images = (int)Math.Max(1, imagesBudget / file);
        return (items, thumbs, images);
    }

    private static void Write(WikiCacheStore store, string key, int size)
    {
        byte[] bytes = new byte[size];
        // 让内容带 key 的痕迹：避免「同内容不同 key」在排查时看不出区别。
        byte[] tag = System.Text.Encoding.UTF8.GetBytes(key);
        Array.Copy(tag, bytes, Math.Min(tag.Length, bytes.Length));

        if (!store.PutVerified(key, bytes, etag: null, expectedSha256: null))
        {
            throw new IOException($"写入失败（磁盘空间不足？）：{key} {size} B");
        }
    }

    private static long BytesOf(IReadOnlyList<WikiCacheTierUsage> tiers, string name)
        => tiers.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.Ordinal))?.Bytes ?? 0;

    private static TierSnapshot ToTier(WikiCacheTierUsage usage) => new()
    {
        Name = usage.Name,
        Files = usage.Files,
        Bytes = usage.Bytes,
        LimitBytes = usage.LimitBytes,
        Protected = usage.Protected,
    };

    private sealed record Options
    {
        public string Root { get; init; } = string.Empty;
        public string Out { get; init; } = "reports/rollout-probe-cache.json";
        public long LimitBytes { get; init; } = 1L * 1024 * 1024 * 1024;
        public long TargetBytes { get; init; } = 1536L * 1024 * 1024;
        public long FileBytes { get; init; } = 32L * 1024 * 1024;
        public string[] Scenarios { get; init; } = ["tier-quota", "global-lru"];
        public bool ShowHelp { get; init; }

        public static Options Parse(string[] args)
        {
            Options defaults = new();
            string root = string.Empty;
            string output = defaults.Out;
            long limit = defaults.LimitBytes;
            long target = defaults.TargetBytes;
            long file = defaults.FileBytes;
            string[] scenarios = defaults.Scenarios;
            bool help = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{a} 缺少值");

                switch (a)
                {
                    case "--root": root = Next(); break;
                    case "--out": output = Next(); break;
                    case "--limit-bytes": limit = ParseSize(Next()); break;
                    case "--target-bytes": target = ParseSize(Next()); break;
                    case "--file-bytes": file = ParseSize(Next()); break;
                    case "--scenarios": scenarios = Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); break;
                    case "-h":
                    case "--help": help = true; break;
                    default: throw new ArgumentException($"未知参数 {a}");
                }
            }

            if (!help)
            {
                if (string.IsNullOrWhiteSpace(root))
                {
                    throw new ArgumentException("必须指定 --root（空目录，工具不会删除已有内容）");
                }

                if (target <= limit)
                {
                    throw new ArgumentException($"target-bytes({target}) 必须大于 limit-bytes({limit})，否则不会触发淘汰");
                }

                if (file <= 0 || limit <= 0)
                {
                    throw new ArgumentException("--file-bytes / --limit-bytes 必须为正数");
                }
            }

            return new Options
            {
                Root = root,
                Out = output,
                LimitBytes = limit,
                TargetBytes = target,
                FileBytes = file,
                Scenarios = scenarios,
                ShowHelp = help,
            };
        }

        /// <summary>支持 1GiB / 512MiB / 1500MB / 纯字节。</summary>
        private static long ParseSize(string text)
        {
            string s = text.Trim();
            (string suffix, long factor)[] units =
            [
                ("GiB", 1024L * 1024 * 1024),
                ("MiB", 1024L * 1024),
                ("KiB", 1024L),
                ("GB", 1000L * 1000 * 1000),
                ("MB", 1000L * 1000),
                ("KB", 1000L),
            ];

            foreach ((string suffix, long factor) in units)
            {
                if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    string number = s[..^suffix.Length].Trim();
                    return (long)(double.Parse(number, CultureInfo.InvariantCulture) * factor);
                }
            }

            return long.Parse(s, CultureInfo.InvariantCulture);
        }

        public static void PrintUsage()
        {
            Console.WriteLine("""
                EllenCacheDrill —— G6 缓存演练（调用客户端真实 Prune）

                用法：
                  dotnet run --project tools/EllenCacheDrill -- --root <空目录> [选项]

                选项：
                  --root <dir>          必填。空目录；工具不会删除已有内容，非空直接退出 2
                  --out <file>          报告路径，默认 reports/rollout-probe-cache.json
                  --limit-bytes <size>  全局上限，默认 1GiB
                  --target-bytes <size> 构造总量，默认 1536MiB（必须大于 limit）
                  --file-bytes <size>   单个文件大小，默认 32MiB
                  --scenarios <list>    tier-quota,global-lru（默认两个都跑）

                退出码：0 成功；2 参数/环境错误；3 断言失败（不写正式报告）

                注意：会真实写入 target-bytes 的磁盘数据（NTFS 上 SetLength 不稀疏在这里用不上，
                      因为 Put 直接写字节）。跑完请自行清理 root。
                """);
        }
    }
}

internal sealed class Payload
{
    [JsonPropertyName("schema")] public string Schema { get; set; } = string.Empty;
    [JsonPropertyName("generatedAt")] public string GeneratedAt { get; set; } = string.Empty;
    [JsonPropertyName("verifiedTarget")] public string VerifiedTarget { get; set; } = string.Empty;
    [JsonPropertyName("provenance")] public string Provenance { get; set; } = string.Empty;
    [JsonPropertyName("realTraffic")] public bool RealTraffic { get; set; }
    [JsonPropertyName("note")] public string Note { get; set; } = string.Empty;
    [JsonPropertyName("limitBytes")] public long LimitBytes { get; set; }
    [JsonPropertyName("targetBytes")] public long TargetBytes { get; set; }
    [JsonPropertyName("fileBytes")] public long FileBytes { get; set; }
    [JsonPropertyName("scenarios")] public List<ScenarioReport> Scenarios { get; set; } = [];
    [JsonPropertyName("pruneBoundaryOk")] public bool PruneBoundaryOk { get; set; }
    [JsonPropertyName("isolationOk")] public bool IsolationOk { get; set; }
}

internal sealed class ScenarioReport
{
    [JsonPropertyName("scenario")] public string Scenario { get; set; } = string.Empty;
    [JsonPropertyName("limitBytes")] public long LimitBytes { get; set; }
    [JsonPropertyName("bytesBefore")] public long BytesBefore { get; set; }
    [JsonPropertyName("bytesAfter")] public long BytesAfter { get; set; }
    [JsonPropertyName("remainingFiles")] public int RemainingFiles { get; set; }
    [JsonPropertyName("removedUnreferenced")] public int RemovedUnreferenced { get; set; }
    [JsonPropertyName("removedByTier")] public int RemovedByTier { get; set; }
    [JsonPropertyName("removedByLru")] public int RemovedByLru { get; set; }
    [JsonPropertyName("tiersBefore")] public List<TierSnapshot> TiersBefore { get; set; } = [];
    [JsonPropertyName("tiersAfter")] public List<TierSnapshot> TiersAfter { get; set; } = [];
    [JsonPropertyName("isolationOk")] public bool IsolationOk { get; set; }
    [JsonPropertyName("itemsIntact")] public bool ItemsIntact { get; set; }
    [JsonPropertyName("thumbsIntact")] public bool ThumbsIntact { get; set; }
    [JsonPropertyName("infraIntact")] public bool InfraIntact { get; set; }
}

internal sealed class TierSnapshot
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("files")] public int Files { get; set; }
    [JsonPropertyName("bytes")] public long Bytes { get; set; }
    [JsonPropertyName("limitBytes")] public long LimitBytes { get; set; }
    [JsonPropertyName("protected")] public bool Protected { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Payload))]
internal sealed partial class JsonContext : JsonSerializerContext
{
}
