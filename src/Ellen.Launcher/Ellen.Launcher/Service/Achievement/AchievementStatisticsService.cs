// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.ViewModel.Achievement;
using System.Collections.Immutable;
using EntityAchievement = Ellen.Launcher.Model.Entity.Achievement;

namespace Ellen.Launcher.Service.Achievement;

[Service(ServiceLifetime.Transient, typeof(IAchievementStatisticsService))]
public sealed partial class AchievementStatisticsService : IAchievementStatisticsService
{
    private const int AchievementCardTakeCount = 2;

    private readonly IAchievementRepository achievementRepository;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial AchievementStatisticsService(IServiceProvider serviceProvider);

    public async ValueTask<ImmutableArray<AchievementStatistics>> GetAchievementStatisticsAsync(AchievementServiceMetadataContext context, CancellationToken token = default)
    {
        await taskContext.SwitchToBackgroundAsync();
        return SynchronizedGetAchievementStatistics(context);
    }

    private ImmutableArray<AchievementStatistics> SynchronizedGetAchievementStatistics(AchievementServiceMetadataContext context)
    {
        ImmutableArray<AchievementStatistics>.Builder results = ImmutableArray.CreateBuilder<AchievementStatistics>();
        foreach (AchievementArchive archive in achievementRepository.GetAchievementArchiveImmutableArray())
        {
            int finishedCount = achievementRepository.GetFinishedAchievementCountByArchiveId(archive.InnerId);
            int totalCount = context.IdAchievementMap.Count;
            ImmutableArray<EntityAchievement> achievements = achievementRepository.GetLatestFinishedAchievementImmutableArrayByArchiveId(archive.InnerId, AchievementCardTakeCount);

            results.Add(new()
            {
                DisplayName = archive.Name,
                FinishDescription = AchievementStatistics.Format(finishedCount, totalCount, out _),
                Achievements = achievements.SelectAsArray(static (entity, context) => AchievementView.Create(entity, context.IdAchievementMap[entity.Id]), context),
            });
        }

        return results.ToImmutable();
    }
}