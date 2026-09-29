// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Endgame;
using System.Collections.Immutable;
using System.IO;

namespace Ellen.Launcher.Service.Endgame;

/// <summary>
/// 从应用目录的 JSON 资源懒加载幽境 Boss 攻略。
/// <para>
/// 与元数据不同，这份资料是社区整理的攻略文本，随应用发布，因此完全离线可用。
/// 资源缺失或格式错误时只记录日志并返回空集合，不影响页面的既有功能。
/// </para>
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(IHardChallengeGuideService))]
public sealed partial class HardChallengeGuideService : IHardChallengeGuideService
{
    private const string ResourceRelativePath = "Resource/Endgame/HardChallengeBosses.json";

    private readonly ILogger<HardChallengeGuideService> logger;

    private ImmutableArray<HardChallengeBossGuidePeriod>? periods;

    [GeneratedConstructor]
    public partial HardChallengeGuideService(IServiceProvider serviceProvider);

    public ValueTask<ImmutableArray<HardChallengeBossGuidePeriod>> GetPeriodsAsync()
    {
        return periods is { } cached ? ValueTask.FromResult(cached) : LoadAsync();
    }

    private async ValueTask<ImmutableArray<HardChallengeBossGuidePeriod>> LoadAsync()
    {
        string path = InstalledLocation.GetAbsolutePath(ResourceRelativePath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Hard challenge boss guide file not found: {Path}", path);
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            HardChallengeBossGuidePeriod[]? deserialized = await JsonSerializer
                .DeserializeAsync<HardChallengeBossGuidePeriod[]>(stream)
                .ConfigureAwait(false);

            ImmutableArray<HardChallengeBossGuidePeriod> parsed = deserialized is { Length: > 0 }
                ? [.. deserialized]
                : [];

            ImmutableArray<string> sectionTitles =
            [
                SH.ViewPageHardChallengeGuideMechanism,
                SH.ViewPageHardChallengeGuideIntroduction,
                SH.ViewPageHardChallengeGuideBackground,
            ];

            string imageFolder = InstalledLocation.GetAbsolutePath(Path.Combine("Resource", "Endgame", "HardChallenge"));

            foreach (HardChallengeBossGuidePeriod period in parsed)
            {
                foreach (HardChallengeBossGuideBoss boss in period.Bosses)
                {
                    boss.Parse(sectionTitles, imageFolder);
                }
            }

            periods = parsed;
            return parsed;
        }
        catch (Exception ex)
        {
            // 攻略资源属于“锦上添花”，任何读取/解析失败都不应影响页面的既有功能。
            logger.LogError(ex, "Failed to load hard challenge boss guide from {Path}", path);
            return [];
        }
    }
}
