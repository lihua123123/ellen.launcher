using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ellen.Launcher.Domain.GameRecord;

namespace Ellen.Launcher.Test.GameRecord;

/// <summary>
/// P1-B fixture：期次登记 / 排序 / 命名 / 默认选中的策略。
/// </summary>
/// <remarks>
/// 数据来自随包的 TowerSchedule.json / RoleCombatSchedule.json / HardChallengeSchedule.json，
/// 策略函数就是 Service 与 ViewModel 运行时调用的同一份（Ellen.Launcher.Domain.GameRecord.ScheduleTimeline），
/// 不是测试里另写一套“等价实现”。
/// 唯一在测试侧复刻的是 Service 里「先登记再定位」的三步编排（见 <see cref="Register"/>），
/// 因为那三步操作的是各 Service 自己的不可变字典（强类型 Id），
/// 这里用 <see cref="SchedulePeriod"/> 列表做同样的编排，差异只在存储类型。
/// </remarks>
[TestClass]
public class ScheduleTimelineTest
{
    private static readonly TimeSpan Utc8 = TimeSpan.FromHours(8);

    /// <summary>深境第 125 期：随包表止于 124（2026-11-16 03:59:59 结束），下一期官方开在 2026-11-16 04:00。</summary>
    private static readonly SchedulePeriod UnknownAbyss = new(125, new DateTimeOffset(2026, 11, 16, 4, 0, 0, Utc8));

    /// <summary>剧诗第 32 期：随包表止于 31（2026-12-01 03:59:59 结束）。</summary>
    private static readonly SchedulePeriod UnknownTheater = new(32, new DateTimeOffset(2026, 12, 1, 4, 0, 0, Utc8));

    /// <summary>幽境 5269013：随包表止于 5269012（2026-11-11 09:59:59 结束），且接口给的名称为空——就是被移出默认包的那一期。</summary>
    private static readonly SchedulePeriod UnknownHardChallenge = new(5269013, new DateTimeOffset(2026, 11, 11, 10, 0, 0, Utc8));

    [TestMethod]
    public void UnknownSpiralAbyssPeriodIsRegisteredOnce()
    {
        List<ScheduleDto> table = Load("TowerSchedule.json");
        Console.WriteLine($"TowerSchedule: {table.Count} 期，Id {table[0].Id}..{table[^1].Id}，最后一期 End={table[^1].Stop:o}");

        // 与 Service 一致：先按 (Begin desc, Id desc) 排好，再往里插新期
        List<SchedulePeriod> before = ScheduleTimeline.SortDescending(table.Select(static row => new SchedulePeriod(row.Id, row.Start)));

        List<SchedulePeriod> afterFirst = Register(before, UnknownAbyss, out bool addedFirst);
        Console.WriteLine($"登记 {UnknownAbyss.Id} 后：{afterFirst.Count} 期，首位={afterFirst[0].Id}（Begin={afterFirst[0].Begin:o}）");

        Assert.IsTrue(addedFirst, "表外新期必须登记");
        Assert.AreEqual(table.Count + 1, afterFirst.Count, "登记后应多出一期，且只多一期");
        Assert.AreEqual(UnknownAbyss.Id, afterFirst[0].Id, "新期开始时间最晚，必须排在最前");
        Assert.AreEqual(table[^1].Start, afterFirst.Single(static p => p.Id == 124).Begin, "已有期的开始时间不得被覆盖");
        CollectionAssert.AreEqual(
            table.Select(static row => row.Id).OrderByDescending(static id => id).ToArray(),
            afterFirst.Skip(1).Select(static p => p.Id).ToArray(),
            "除新期外，其余期次顺序必须与登记前一致");

        // 幂等：同一期重复登记不得产生第二张卡片
        List<SchedulePeriod> afterSecond = Register(afterFirst, UnknownAbyss, out bool addedSecond);
        Console.WriteLine($"重复登记同一期：added={addedSecond}，仍为 {afterSecond.Count} 期");

        Assert.IsFalse(addedSecond, "同一 ScheduleId 重复登记必须是空操作");
        Assert.AreEqual(afterFirst.Count, afterSecond.Count, "重复登记不得改变数量");
        CollectionAssert.AreEqual(
            afterFirst.Select(static p => p.Id).ToArray(),
            afterSecond.Select(static p => p.Id).ToArray(),
            "重复登记不得改变顺序");
    }

    [TestMethod]
    public void UnknownRoleCombatPeriodIsRegisteredOnce()
    {
        List<ScheduleDto> table = Load("RoleCombatSchedule.json");
        Console.WriteLine($"RoleCombatSchedule: {table.Count} 期，Id {table[0].Id}..{table[^1].Id}，最后一期 End={table[^1].Stop:o}");

        // 与 Service 一致：先按 (Begin desc, Id desc) 排好，再往里插新期
        List<SchedulePeriod> before = ScheduleTimeline.SortDescending(table.Select(static row => new SchedulePeriod(row.Id, row.Start)));

        List<SchedulePeriod> after = Register(before, UnknownTheater, out bool added);

        Assert.IsTrue(added, "表外新期必须登记");
        Assert.AreEqual(table.Count + 1, after.Count);
        Assert.AreEqual(UnknownTheater.Id, after[0].Id);
        CollectionAssert.AreEqual(
            table.Select(static row => row.Id).OrderByDescending(static id => id).ToArray(),
            after.Skip(1).Select(static p => p.Id).ToArray());

        List<SchedulePeriod> again = Register(after, UnknownTheater, out bool addedAgain);
        Assert.IsFalse(addedAgain);
        Assert.AreEqual(after.Count, again.Count);

        // 剧诗新期没有元素 / 特邀名单（接口不给）：登记进去只带起止时间，不会伪造名单，
        // 这里用「周期出现 + 顺序正确」证明卡片能上屏，名单留空由 Service 侧负责。
        Console.WriteLine($"登记 {UnknownTheater.Id} 后：{again.Count} 期，首位={again[0].Id}");
    }

    [TestMethod]
    public void UnknownHardChallengePeriodIsRegisteredOnce()
    {
        List<ScheduleDto> table = Load("HardChallengeSchedule.json");
        Console.WriteLine($"HardChallengeSchedule: {table.Count} 期，Id {table[0].Id}..{table[^1].Id}，最后一期 Name=\"{table[^1].Name}\" End={table[^1].Stop:o}");

        // 与 Service 一致：先按 (Begin desc, Id desc) 排好，再往里插新期
        List<SchedulePeriod> before = ScheduleTimeline.SortDescending(table.Select(static row => new SchedulePeriod(row.Id, row.Start)));

        List<SchedulePeriod> after = Register(before, UnknownHardChallenge, out bool added);

        Assert.IsTrue(added);
        Assert.AreEqual(table.Count + 1, after.Count);
        Assert.AreEqual(UnknownHardChallenge.Id, after[0].Id);
        Assert.AreEqual(UnknownHardChallenge.Begin, after[0].Begin, "新期开始时间必须取接口的官方时间");

        List<SchedulePeriod> again = Register(after, UnknownHardChallenge, out bool addedAgain);
        Assert.IsFalse(addedAgain);
        Assert.AreEqual(after.Count, again.Count);

        // 这一期接口名称为空 → 生效名称为空串 → 视图走「第 13 期：未命名」兜底（不伪造名字）
        Assert.AreEqual(string.Empty, ScheduleTimeline.ResolveName(null, string.Empty));

        Console.WriteLine($"登记 {UnknownHardChallenge.Id} 后：{again.Count} 期，首位={again[0].Id}，生效名称=\"{ScheduleTimeline.ResolveName(null, string.Empty)}\"");
    }

    [TestMethod]
    public void EmptyNameFallsBackToApiNameThenPlaceholder()
    {
        // 表内为空 → 用接口名
        Assert.AreEqual("诡谲之渊", ScheduleTimeline.ResolveName(string.Empty, "诡谲之渊"));
        Assert.AreEqual("诡谲之渊", ScheduleTimeline.ResolveName("   ", "诡谲之渊"));

        // 表内已有名称 → 接口名不得覆盖（已有期原样保留）
        Assert.AreEqual("烁煌之役", ScheduleTimeline.ResolveName("烁煌之役", "别的名字"));
        Assert.AreEqual("烁煌之役", ScheduleTimeline.ResolveName("烁煌之役", null));

        // 两边都为空 → 空串（交给视图兜底文案，不得留白）
        Assert.AreEqual(string.Empty, ScheduleTimeline.ResolveName(null, null));
        Assert.AreEqual(string.Empty, ScheduleTimeline.ResolveName(string.Empty, "  "));

        Console.WriteLine($"表内空 + 接口名 → \"{ScheduleTimeline.ResolveName(string.Empty, "诡谲之渊")}\"");
        Console.WriteLine($"表内有名 + 接口名 → \"{ScheduleTimeline.ResolveName("烁煌之役", "别的名字")}\"（不被覆盖）");
        Console.WriteLine($"两边都空 → \"{ScheduleTimeline.ResolveName(string.Empty, string.Empty)}\"（视图兜底「第 N 期：未命名」）");
    }

    [TestMethod]
    public void DefaultSelectionPrefersCurrentThenNewestEngagedThenNewest()
    {
        // 空列表
        Assert.AreEqual(-1, ScheduleTimeline.SelectDefaultIndex([], null));

        ScheduleSelection[] items =
        [
            new(125, false),
            new(124, true),
            new(123, false),
            new(122, true),
        ];

        // 用户已经选了 123：刷新不得把它顶掉
        Assert.AreEqual(2, ScheduleTimeline.SelectDefaultIndex(items, 123));
        // 用户选的期不在列表里（换账号 / 期次被换掉）：退到最近有记录的
        Assert.AreEqual(1, ScheduleTimeline.SelectDefaultIndex(items, 999));
        // 没有任何既有选择：最近有记录的
        Assert.AreEqual(1, ScheduleTimeline.SelectDefaultIndex(items, null));
        // 一期记录都没有：最新一期
        Assert.AreEqual(0, ScheduleTimeline.SelectDefaultIndex([new(125, false), new(124, false)], null));

        Console.WriteLine("列表: " + string.Join(", ", items.Select(static i => $"{i.Id}{(i.Engaged ? "(有记录)" : string.Empty)}")));
        Console.WriteLine($"已选 123 → 索引 {ScheduleTimeline.SelectDefaultIndex(items, 123)}（保持）");
        Console.WriteLine($"已选 999（不存在）→ 索引 {ScheduleTimeline.SelectDefaultIndex(items, 999)}（最近有记录）");
        Console.WriteLine($"无既有选择 → 索引 {ScheduleTimeline.SelectDefaultIndex(items, null)}（最近有记录）");
        Console.WriteLine($"全无记录 → 索引 {ScheduleTimeline.SelectDefaultIndex([new(125, false), new(124, false)], null)}（最新一期）");
    }

    /// <summary>
    /// 口径锁定：**最新一期没有个人记录时不强制选中它**，退到「最近一期有记录的」。
    /// </summary>
    /// <remarks>
    /// 这一条是为了防止以后有人把默认选中改成「永远选最新一期」：深境 / 剧诗没有攻略兜底，
    /// 新期一开、当期还没打，首页就会变空白。幽境有攻略模式能兜住，但三页统一走同一个策略，
    /// 所以按账号级口径写死。改这条断言前先想清楚这三页的空态。
    /// </remarks>
    [TestMethod]
    public void NewestPeriodWithoutRecordIsNotForced()
    {
        ScheduleSelection[] items =
        [
            new(125, false), // 最新一期，还没打
            new(124, true),  // 上一期，有记录
        ];

        Assert.AreEqual(1, ScheduleTimeline.SelectDefaultIndex(items, null), "最新一期无记录时不得强制选中它");
        Assert.AreEqual(1, ScheduleTimeline.SelectDefaultIndex(items, 124), "已选上一期时保持");
        Assert.AreEqual(0, ScheduleTimeline.SelectDefaultIndex([.. items], 125), "用户自己点过最新一期则尊重用户选择");

        Console.WriteLine($"最新一期(125)无记录、上一期(124)有记录 → 索引 {ScheduleTimeline.SelectDefaultIndex(items, null)}（不强制最新，避免首页空白）");
    }

    /// <summary>
    /// 口径锁定：表外新期的占位卡**只带官方起止时间**，名称 / 祝福 / 楼层 / 元素 / 特邀名单一律不编。
    /// </summary>
    /// <remarks>
    /// 深境祝福与楼层、剧诗元素与特邀/开幕名单只有随包静态表有（接口不给），
    /// 幽境的攻略卡来自随包资源。这些字段在表外新期上必须是空的 ——
    /// UI 因此会出现「有期次、无内容」的卡片，这是内容缺口，不是用别的期顶替的理由。
    /// </remarks>
    [TestMethod]
    public void PlaceholderCardCarriesOnlyOfficialTimes()
    {
        // 名称：接口不给就留空，视图再套「第 N 期：未命名」，不编名字
        Assert.AreEqual(string.Empty, ScheduleTimeline.ResolveName(null, null));

        // 时间：只回显接口给的官方起止时间
        DateTimeOffset begin = new(2026, 11, 11, 10, 0, 0, Utc8);
        DateTimeOffset end = new(2026, 12, 15, 3, 59, 59, Utc8);
        Assert.AreEqual("2026.11.11 10:00 - 2026.12.15 03:59", ScheduleTimeline.FormatTimeRange(begin, end, "时间未知"));

        // 时间缺失时不补一个「看起来正常」的时间
        Assert.AreEqual("时间未知", ScheduleTimeline.FormatTimeRange(default, default, "时间未知"));

        Console.WriteLine($"占位卡：名称=\"{ScheduleTimeline.ResolveName(null, null)}\"、时间=\"{ScheduleTimeline.FormatTimeRange(begin, end, "时间未知")}\"（其余字段留空）");
    }

    [TestMethod]
    public void ExistingPeriodOrderIsUnchanged()
    {
        // 旧实现是 OrderByDescending(ScheduleId)。新实现按 (Begin desc, Id desc)，
        // 对三张随包表必须逐项一致，否则「已有期排序不变」这条就破了。
        foreach (string fixture in new[] { "TowerSchedule.json", "RoleCombatSchedule.json", "HardChallengeSchedule.json" })
        {
            List<ScheduleDto> table = Load(fixture);
            uint[] legacy = [.. table.Select(static row => row.Id).OrderByDescending(static id => id)];
            uint[] current = [.. ScheduleTimeline.SortDescending(table.Select(static row => new SchedulePeriod(row.Id, row.Start))).Select(static p => p.Id)];

            Console.WriteLine($"{fixture}: {table.Count} 期，旧排序首位={legacy[0]}，新排序首位={current[0]}");

            CollectionAssert.AreEqual(legacy, current, $"{fixture} 的已有期排序必须与 OrderByDescending(Id) 完全一致");
        }
    }

    [TestMethod]
    public void InsertIndexMatchesBeginOrder()
    {
        List<SchedulePeriod> descending =
        [
            new(125, new DateTimeOffset(2026, 11, 16, 4, 0, 0, Utc8)),
            new(124, new DateTimeOffset(2026, 10, 16, 4, 0, 0, Utc8)),
        ];

        // 最晚 → 最前
        Assert.AreEqual(0, ScheduleTimeline.InsertIndex(descending, new(126, new DateTimeOffset(2026, 12, 1, 4, 0, 0, Utc8))));
        // 中间
        Assert.AreEqual(1, ScheduleTimeline.InsertIndex(descending, new(999, new DateTimeOffset(2026, 11, 1, 4, 0, 0, Utc8))));
        // 最早 → 末尾
        Assert.AreEqual(2, ScheduleTimeline.InsertIndex(descending, new(1, new DateTimeOffset(2020, 7, 1, 4, 0, 0, Utc8))));
        // 同一开始时间按 Id 倒序
        Assert.AreEqual(0, ScheduleTimeline.InsertIndex(descending, new(126, descending[0].Begin)));

        Console.WriteLine($"列表: {string.Join(", ", descending.Select(static p => p.Id))}");
        Console.WriteLine($"插入更晚的 126 → 0；插入 2026-11-01 → 1；插入 2020-07-01 → 2；同时间插入 126 → 0");
    }

    [TestMethod]
    public void UnknownTimeIsNotFabricated()
    {
        string unknown = "时间未知";

        Assert.AreEqual(unknown, ScheduleTimeline.FormatTimeRange(default, default, unknown));
        Assert.AreEqual(unknown, ScheduleTimeline.FormatTimeRange(default, new DateTimeOffset(2026, 11, 16, 4, 0, 0, Utc8), unknown));
        Assert.AreEqual(unknown, ScheduleTimeline.FormatTimeRange(new DateTimeOffset(2026, 11, 16, 4, 0, 0, Utc8), default, unknown));

        // 正常值与旧实现的插值结果必须逐字一致
        DateTimeOffset begin = new(2026, 10, 16, 4, 0, 0, Utc8);
        DateTimeOffset end = new(2026, 11, 16, 3, 59, 59, Utc8);
        Assert.AreEqual($"{begin:yyyy.MM.dd HH:mm} - {end:yyyy.MM.dd HH:mm}", ScheduleTimeline.FormatTimeRange(begin, end, unknown));

        Console.WriteLine($"缺失 → \"{ScheduleTimeline.FormatTimeRange(default, default, unknown)}\"");
        Console.WriteLine($"完整 → \"{ScheduleTimeline.FormatTimeRange(begin, end, unknown)}\"");
    }

    [TestMethod]
    public void ZeroScheduleIdIsNotTreatedAsPeriod()
    {
        // 接口在没有当期数据时可能回 schedule_id = 0；登记进去就是一张「第 0 期」的空卡片
        Assert.IsFalse(ScheduleTimeline.IsValidId(0));
        Assert.IsTrue(ScheduleTimeline.IsValidId(125));
        Assert.IsTrue(ScheduleTimeline.IsValidId(5269013));

        List<SchedulePeriod> table = ScheduleTimeline.SortDescending(
            Load("TowerSchedule.json").Select(static row => new SchedulePeriod(row.Id, row.Start)));

        // 与 Service 的守卫一致：0 直接不登记，列表长度不变
        Assert.AreEqual(table.Count, Register(table, new SchedulePeriod(0, default), out bool added).Count);
        Assert.IsFalse(added);

        Console.WriteLine($"IsValidId(0)={ScheduleTimeline.IsValidId(0)}，登记 0 期：added={added}，仍为 {table.Count} 期");
    }

    /// <summary>
    /// 与 Service 中的编排一一对应：先校验期次 Id、再问是否要登记，最后按插入位置写进列表。
    /// </summary>
    private static List<SchedulePeriod> Register(List<SchedulePeriod> descendingPeriods, SchedulePeriod incoming, out bool added)
    {
        if (!ScheduleTimeline.IsValidId(incoming.Id)
            || !ScheduleTimeline.NeedsRegistration(descendingPeriods.Select(static p => p.Id), incoming.Id))
        {
            added = false;
            return descendingPeriods;
        }

        added = true;
        List<SchedulePeriod> updated = new(descendingPeriods);
        updated.Insert(ScheduleTimeline.InsertIndex(descendingPeriods, incoming), incoming);
        return updated;
    }

    private static List<ScheduleDto> Load(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"缺少 fixture：{path}（应随测试项目复制到输出目录）", path);
        }

        List<ScheduleDto> rows = JsonSerializer.Deserialize<List<ScheduleDto>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"{fileName} 解析失败");

        Assert.IsTrue(rows.Count > 0, $"{fileName} 不应为空");
        return rows;
    }

    private sealed class ScheduleDto
    {
        public uint Id { get; set; }

        // 深境用 Open/Close，剧诗与幽境用 Begin/End
        public DateTimeOffset Open { get; set; }

        public DateTimeOffset Close { get; set; }

        public DateTimeOffset Begin { get; set; }

        public DateTimeOffset End { get; set; }

        public string? Name { get; set; }

        public DateTimeOffset Start => Open != default ? Open : Begin;

        public DateTimeOffset Stop => Close != default ? Close : End;
    }
}
