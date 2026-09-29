// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.ViewModel.Cultivation;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.Cultivation;

public static class CultivationServiceExtension
{
    extension(ICultivationService cultivationService)
    {
        public async ValueTask<CultivateProject?> GetCurrentProjectAsync()
        {
            IAdvancedDbCollectionView<CultivateProject> projects = await cultivationService.GetProjectCollectionAsync().ConfigureAwait(false);
            await cultivationService.EnsureCurrentProjectAsync(projects).ConfigureAwait(false);
            return projects.CurrentItem;
        }

        public async ValueTask<ObservableCollection<CultivateEntryView>?> GetCultivateEntryCollectionForCurrentProjectAsync(ICultivationMetadataContext context)
        {
            IAdvancedDbCollectionView<CultivateProject> projects = await cultivationService.GetProjectCollectionAsync().ConfigureAwait(false);
            if (!await cultivationService.EnsureCurrentProjectAsync(projects).ConfigureAwait(false))
            {
                return default;
            }

            ArgumentNullException.ThrowIfNull(projects.CurrentItem);
            return await cultivationService.GetCultivateEntryCollectionAsync(projects.CurrentItem, context).ConfigureAwait(false);
        }
    }
}