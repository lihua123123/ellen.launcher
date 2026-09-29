// 开发期校验工具：验证幽境 Boss 攻略数据在真实模型代码下的解析结果。
// 用法: GuideDataCheck <HardChallengeBosses.json> <图片目录>
using System.Collections.Immutable;
using System.Text.Json;
using Ellen.Launcher.Model.Endgame;

if (args.Length < 2)
{
    Console.WriteLine("用法: GuideDataCheck <HardChallengeBosses.json> <图片目录>");
    return 2;
}

string jsonPath = args[0];
string imageFolder = args[1];

if (!File.Exists(jsonPath))
{
    Console.WriteLine($"FAIL 找不到 JSON: {jsonPath}");
    return 2;
}

HardChallengeBossGuidePeriod[]? periods;
try
{
    periods = JsonSerializer.Deserialize<HardChallengeBossGuidePeriod[]>(File.ReadAllText(jsonPath));
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL 反序列化异常: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

if (periods is null)
{
    Console.WriteLine("FAIL 反序列化结果为 null");
    return 1;
}

ImmutableArray<string> sectionTitles = ["机制", "介绍", "背景"];

int bossCount = 0;
int sectionCount = 0;
int lineCount = 0;
int headingCount = 0;
int imageOk = 0;
int hpOk = 0;
int mechanismOk = 0;
int detailOk = 0;
List<string> missingImages = [];
List<string> emptySections = [];
List<string> emptyPeriodVersions = [];
List<string> missingHp = [];
List<string> missingMechanism = [];
List<string> missingDetail = [];

foreach (HardChallengeBossGuidePeriod period in periods)
{
    if (string.IsNullOrEmpty(period.Version))
    {
        emptyPeriodVersions.Add("<空>");
    }

    foreach (HardChallengeBossGuideBoss boss in period.Bosses)
    {
        boss.Parse(sectionTitles, imageFolder);
        bossCount++;
        sectionCount += boss.Sections.Length;

        foreach (HardChallengeGuideSection section in boss.Sections)
        {
            lineCount += section.Lines.Length;
            headingCount += section.Lines.Count(static l => l.IsHeading);
        }

        if (boss.Sections.Length is 0)
        {
            emptySections.Add($"{period.Version}/{boss.FullName}");
        }

        if (boss.HasImage)
        {
            imageOk++;
        }
        else
        {
            missingImages.Add($"{period.Version}/{boss.ImageFileName ?? "<无图名>"}");
        }

        if (boss.HasHp)
        {
            hpOk++;
        }
        else
        {
            missingHp.Add($"{period.Version}/{boss.ShortName}");
        }

        if (boss.HasMechanism)
        {
            mechanismOk++;
        }
        else
        {
            missingMechanism.Add($"{period.Version}/{boss.ShortName}");
        }

        if (boss.HasDetail)
        {
            detailOk++;
        }
        else
        {
            missingDetail.Add($"{period.Version}/{boss.ShortName}");
        }
    }
}

HardChallengeBossGuidePeriod last = periods[^1];

Console.WriteLine($"期数            : {periods.Length}");
Console.WriteLine($"Boss 总数       : {bossCount}");
Console.WriteLine($"段落总数        : {sectionCount}");
Console.WriteLine($"文本行总数      : {lineCount}（其中小标题 {headingCount}）");
Console.WriteLine($"图片可用/缺失   : {imageOk} / {missingImages.Count}");
Console.WriteLine($"血量(N5/N6)解析 : {hpOk} / {bossCount}");
Console.WriteLine($"机制段可用      : {mechanismOk} / {bossCount}");
Console.WriteLine($"介绍+背景段可用 : {detailOk} / {bossCount}");
Console.WriteLine($"最新一期        : {last.Version}（{last.Bosses.Length} 个 Boss）");
Console.WriteLine();

foreach (HardChallengeBossGuidePeriod period in periods)
{
    Console.WriteLine($"  {period.Version,-6} bosses={period.Bosses.Length}");
}

Console.WriteLine();
foreach (HardChallengeBossGuideBoss boss in last.Bosses)
{
    Console.WriteLine($"[{last.Version}] {boss.FullName} / {boss.ShortName}");
    Console.WriteLine($"    HP   : N5={boss.HpN5}  N6={boss.HpN6}（原始 {boss.Hp}）");
    Console.WriteLine($"    图片 : {(boss.ImageUri?.LocalPath ?? "<缺失>")}");
    Console.WriteLine($"    副卡 : {boss.DetailSections.Length} 段（{string.Join('/', boss.DetailSections.Select(s => s.Title))}）");
    foreach (HardChallengeGuideSection section in boss.Sections)
    {
        Console.WriteLine($"    段[{section.Title}] {section.Lines.Length} 行");
        foreach (HardChallengeGuideLine line in section.Lines.Take(3))
        {
            string text = line.Text.Length > 60 ? line.Text[..60] + "…" : line.Text;
            Console.WriteLine($"        {(line.IsHeading ? "## " : "   ")}{text}");
        }
    }

    Console.WriteLine();
}

List<string> failures = [];
if (periods.Length is 0)
{
    failures.Add("期数为 0");
}

if (emptyPeriodVersions.Count > 0)
{
    failures.Add($"存在空版本号 {emptyPeriodVersions.Count} 个");
}

if (emptySections.Count > 0)
{
    failures.Add($"以下 Boss 无正文: {string.Join(", ", emptySections)}");
}

if (lineCount is 0)
{
    failures.Add("正文行为 0");
}

if (missingHp.Count > 0)
{
    failures.Add($"血量解析失败 {missingHp.Count} 个: {string.Join(", ", missingHp)}");
}

if (missingMechanism.Count > 0)
{
    failures.Add($"缺少机制段: {string.Join(", ", missingMechanism)}");
}

if (missingDetail.Count > 0)
{
    failures.Add($"缺少介绍/背景段: {string.Join(", ", missingDetail)}");
}

Console.WriteLine("==== 结论 ====");
if (failures.Count is 0)
{
    Console.WriteLine($"PASS 数据可用（{periods.Length} 期 / {bossCount} Boss / {lineCount} 行正文，血量与机制、介绍背景均可用，{missingImages.Count} 张配图缺失）");
    if (missingImages.Count > 0)
    {
        Console.WriteLine($"提示 缺失配图: {string.Join(", ", missingImages)}");
    }

    return 0;
}

foreach (string failure in failures)
{
    Console.WriteLine($"FAIL {failure}");
}

return 1;
