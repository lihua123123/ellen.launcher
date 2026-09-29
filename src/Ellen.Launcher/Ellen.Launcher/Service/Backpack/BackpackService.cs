// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Core.LifeCycle.InterProcess.Yae;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Service.Yae.PlayerStore;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Backpack;

[Service(ServiceLifetime.Scoped, typeof(IBackpackService))]
public sealed partial class BackpackService : IBackpackService
{
    private readonly IBackpackRepository backpackRepository;
    private readonly IServiceProvider serviceProvider;

    [GeneratedConstructor]
    public partial BackpackService(IServiceProvider serviceProvider);

    private readonly AsyncLock archivesLock = new();

    private IAdvancedDbCollectionView<BackpackArchive>? archives;

    public async ValueTask<IAdvancedDbCollectionView<BackpackArchive>> GetArchiveCollectionAsync()
    {
        using (await archivesLock.LockAsync().ConfigureAwait(false))
        {
            return archives ??= backpackRepository.GetBackpackArchiveCollection()
                              .ToAdvancedDbCollectionView(serviceProvider);
        }
    }

    public BackpackArchive AddArchive(string name)
    {
        BackpackArchive archive = BackpackArchive.Create(name);
        backpackRepository.AddBackpackArchive(archive);

        archives = null;

        return archive;
    }

    public bool RefreshByEmbeddedYae(BackpackArchive archive, PlayerStoreResult storeResult)
    {
        ArgumentNullException.ThrowIfNull(storeResult.StoreBytes);
        ImmutableArray<BackpackItem> items = PlayerStoreParser.ParseToBackpackItems(storeResult.StoreBytes, archive.InnerId);

        // Add virtual currency items from player properties
        ImmutableArray<BackpackItem> virtualItems = CreateVirtualItems(archive.InnerId, storeResult.PropMap);

        backpackRepository.RemoveBackpackItemRangeByArchiveId(archive.InnerId);
        backpackRepository.AddBackpackItemRange(items);
        backpackRepository.AddBackpackItemRange(virtualItems);

        return true;
    }

    public void RemoveArchive(BackpackArchive archive)
    {
        backpackRepository.RemoveBackpackItemRangeByArchiveId(archive.InnerId);
        backpackRepository.RemoveBackpackArchiveById(archive.InnerId);

        archives = null;
    }

    public ImmutableArray<BackpackItem> GetBackpackItemImmutableArrayByArchiveId(Guid archiveId)
    {
        return backpackRepository.GetBackpackItemImmutableArrayByArchiveId(archiveId);
    }

    private static ImmutableArray<BackpackItem> CreateVirtualItems(Guid archiveId, Dictionary<InterestedPropType, double> propMap)
    {
        ImmutableArray<BackpackItem>.Builder builder = ImmutableArray.CreateBuilder<BackpackItem>();

        AddIfPositive(ItemIds.Primogem, propMap.GetValueOrDefault(InterestedPropType.PlayerHCoin) - propMap.GetValueOrDefault(InterestedPropType.PlayerWaitSubHCoin));
        AddIfPositive(ItemIds.Mora, propMap.GetValueOrDefault(InterestedPropType.PlayerSCoin) - propMap.GetValueOrDefault(InterestedPropType.PlayerWaitSubSCoin));
        AddIfPositive(ItemIds.GenesisCrystal, propMap.GetValueOrDefault(InterestedPropType.PlayerMCoin) - propMap.GetValueOrDefault(InterestedPropType.PlayerWaitSubMCoin));
        AddIfPositive(ItemIds.RealmCurrency, propMap.GetValueOrDefault(InterestedPropType.PlayerHomeCoin) - propMap.GetValueOrDefault(InterestedPropType.PlayerWaitSubHomeCoin));
        AddIfPositive(ItemIds.ToyMedal, propMap.GetValueOrDefault(InterestedPropType.PlayerRoleCombatCoin));
        AddIfPositive(ItemIds.RepertoireCoins, propMap.GetValueOrDefault(InterestedPropType.PlayerMusicGameBookCoin));

        return builder.ToImmutable();

        void AddIfPositive(uint itemId, double count)
        {
            if (count > 0)
            {
                builder.Add(new BackpackItem
                {
                    ArchiveId = archiveId,
                    ItemId = itemId,
                    Count = (uint)Math.Clamp(count, uint.MinValue, uint.MaxValue),
                });
            }
        }
    }
}
