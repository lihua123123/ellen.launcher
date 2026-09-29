// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Diagnostics;
using Ellen.Launcher.Service.AvatarInfo.Factory;
using Ellen.Launcher.ViewModel.AvatarProperty;
using Ellen.Launcher.ViewModel.User;
using System.Collections.Immutable;
using EntityAvatarInfo = Ellen.Launcher.Model.Entity.AvatarInfo;

namespace Ellen.Launcher.Service.AvatarInfo;

[Service(ServiceLifetime.Scoped, typeof(IAvatarInfoService))]
public sealed partial class AvatarInfoService : IAvatarInfoService
{
    private readonly AvatarInfoRepositoryOperation avatarInfoDbBulkOperation;
    private readonly IAvatarInfoRepository avatarInfoRepository;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly ILogger<AvatarInfoService> logger;

    [GeneratedConstructor]
    public partial AvatarInfoService(IServiceProvider serviceProvider);

    public async ValueTask<Summary?> GetSummaryAsync(SummaryFactoryMetadataContext context, UserAndUid userAndUid, RefreshOptionKind refreshOptionKind, CancellationToken token = default)
    {
        switch (refreshOptionKind)
        {
            case RefreshOptionKind.RequestFromHoyolabGameRecord:
                {
                    ImmutableArray<EntityAvatarInfo> list = await avatarInfoDbBulkOperation.UpdateDbAvatarInfosAsync(userAndUid, token).ConfigureAwait(false);
                    Summary summary = await PrivateGetSummaryAsync(context, list, token).ConfigureAwait(false);
                    return summary;
                }

            default:
                {
                    ImmutableArray<EntityAvatarInfo> list = avatarInfoRepository.GetAvatarInfoImmutableArrayByUid(userAndUid.Uid.Value);
                    Summary summary = await PrivateGetSummaryAsync(context, list, token).ConfigureAwait(false);
                    return summary.Avatars is [] ? null : summary;
                }
        }
    }

    private async ValueTask<Summary> PrivateGetSummaryAsync(SummaryFactoryMetadataContext context, ImmutableArray<EntityAvatarInfo> avatarInfos, CancellationToken token)
    {
        using (IServiceScope scope = serviceScopeFactory.CreateScope())
        {
            ISummaryFactory summaryFactory = scope.ServiceProvider.GetRequiredService<ISummaryFactory>();

            using (ValueStopwatch.MeasureExecution(logger))
            {
                return await summaryFactory.CreateAsync(context, avatarInfos, token).ConfigureAwait(false);
            }
        }
    }
}