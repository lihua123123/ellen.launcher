// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.SpiralAbyss;
using Ellen.Launcher.ViewModel.User;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.SpiralAbyss;

public interface ISpiralAbyssService
{
    ValueTask<ObservableCollection<SpiralAbyssView>> GetSpiralAbyssViewCollectionAsync(SpiralAbyssMetadataContext context, UserAndUid userAndUid);

    ValueTask RefreshSpiralAbyssAsync(SpiralAbyssMetadataContext context, UserAndUid userAndUid);
}