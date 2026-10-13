// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.DependencyInjection.Abstraction;
using Ellen.Launcher.Model.Entity.Database;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.Avatar;
using Ellen.Launcher.Web.Response;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using CalculableAvatar = Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate.Avatar;
using EntityAvatarInfo = Ellen.Launcher.Model.Entity.AvatarInfo;

namespace Ellen.Launcher.Service.AvatarInfo;

[Service(ServiceLifetime.Singleton)]
public sealed partial class AvatarInfoRepositoryOperation
{
    private readonly IAvatarInfoRepository avatarInfoRepository;
    private readonly IServiceProvider serviceProvider;
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial AvatarInfoRepositoryOperation(IServiceProvider serviceProvider);

    public async ValueTask<ImmutableArray<EntityAvatarInfo>> UpdateDbAvatarInfosAsync(UserAndUid userAndUid, CancellationToken token)
    {
        await taskContext.SwitchToBackgroundAsync();
        string uid = userAndUid.Uid.Value;
        ImmutableArray<EntityAvatarInfo> dbInfos = avatarInfoRepository.GetAvatarInfoImmutableArrayByUid(uid);
        EnsureItemsAvatarIdUnique(dbInfos, uid, out FrozenDictionary<AvatarId, EntityAvatarInfo> dbInfoMap);

        using (IServiceScope scope = serviceProvider.CreateScope())
        {
            AppDbContext appDbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            IGameRecordClient gameRecordClient = serviceProvider
                .GetRequiredService<IOverseaSupportFactory<IGameRecordClient>>()
                .CreateFor(userAndUid);

            CalculateClient calculateClient = scope.ServiceProvider.GetRequiredService<CalculateClient>();

            // This is a tricky way to immediately update the avatar info, this behavior
            // can change in the future by miHoYo, so it's not recommended to rely on this.
            await gameRecordClient.GetPlayerInfoAsync(userAndUid, token).ConfigureAwait(false);

            Response<ListWrapper<Character>> listResponse = await gameRecordClient
                .GetCharacterListAsync(userAndUid, token)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(listResponse, serviceProvider, out ListWrapper<Character>? charactersWrapper))
            {
                return avatarInfoRepository.GetAvatarInfoImmutableArrayByUid(uid);
            }

            ImmutableArray<AvatarId> characterIds = charactersWrapper.List.SelectAsArray(static info => info.Id);
            Response<ListWrapper<DetailedCharacter>> detailResponse = await gameRecordClient
                .GetCharacterDetailAsync(userAndUid, characterIds, token)
                .ConfigureAwait(false);

            if (!ResponseValidator.TryValidate(detailResponse, serviceProvider, out ListWrapper<DetailedCharacter>? detailsWrapper))
            {
                return avatarInfoRepository.GetAvatarInfoImmutableArrayByUid(uid);
            }

            // We can't obtain avatar promote level from game record,
            // but we can obtain it from the calculator.
            ImmutableArray<CalculableAvatar> calculableAvatars = await calculateClient.GetAvatarsAsync(userAndUid, token).ConfigureAwait(false);
            ImmutableDictionary<uint, CalculableAvatar> calculableAvatarMap = calculableAvatars.ToImmutableDictionary(x => x.Id);

            foreach (DetailedCharacter character in detailsWrapper.List)
            {
                // 旅行者也入库，以便「我的角色」页能展示旅行者的面板
                CalculableAvatar? calculableAvatar = calculableAvatarMap.GetValueOrDefault(character.Base.Id);
                character.Base.PromoteLevel = calculableAvatar?.PromoteLevel ?? 0;

                EntityAvatarInfo? entity = dbInfoMap.GetValueOrDefault(character.Base.Id);
                AddOrUpdateAvatarInfo(entity, uid, appDbContext, character);
            }
        }

        return avatarInfoRepository.GetAvatarInfoImmutableArrayByUid(uid);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddOrUpdateAvatarInfo(EntityAvatarInfo? entity, string uid, AppDbContext appDbContext, DetailedCharacter info)
    {
        if (entity is null)
        {
            entity = EntityAvatarInfo.From(uid, info);
        }
        else
        {
            entity.Info2 = info;
        }

        appDbContext.AvatarInfos.UpdateAndSave(entity);
    }

    private void EnsureItemsAvatarIdUnique(ImmutableArray<EntityAvatarInfo> dbInfos, string uid, out FrozenDictionary<AvatarId, EntityAvatarInfo> dbInfoMap)
    {
        Dictionary<AvatarId, EntityAvatarInfo> infoMap = [];
        foreach (EntityAvatarInfo info in dbInfos)
        {
            if (info.Info2 is null || !infoMap.TryAdd(info.Info2.Base.Id, info))
            {
                avatarInfoRepository.RemoveAvatarInfoRangeByUid(uid);
                dbInfoMap = FrozenDictionary<AvatarId, EntityAvatarInfo>.Empty;
                return;
            }
        }

        dbInfoMap = infoMap.ToFrozenDictionary();
    }
}