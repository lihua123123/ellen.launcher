// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data;
using Ellen.Launcher.ViewModel.AvatarProperty;

namespace Ellen.Launcher.Service.AvatarInfo.Factory;

[Service(ServiceLifetime.Transient, typeof(ISummaryFactory))]
public sealed partial class SummaryFactory : ISummaryFactory
{
    private readonly ITaskContext taskContext;

    [GeneratedConstructor]
    public partial SummaryFactory(IServiceProvider serviceProvider);

    public async ValueTask<Summary> CreateAsync(SummaryFactoryMetadataContext context, IEnumerable<Model.Entity.AvatarInfo> avatarInfos, CancellationToken token)
    {
        await taskContext.SwitchToBackgroundAsync();

        IOrderedEnumerable<AvatarView> avatars = avatarInfos
            .Where(a => a.Info2 is not null)
            .Select(a => SummaryAvatarFactory.Create(context, a))
            .OrderByDescending(a => a.Quality)
            .ThenByDescending(a => a.LevelNumber)
            .ThenBy(a => a.Element)
            .ThenBy(a => a.Weapon?.WeaponType)
            .ThenByDescending(a => a.FetterLevel);

        return Summary.Create(avatars.AsAdvancedCollectionView());
    }
}