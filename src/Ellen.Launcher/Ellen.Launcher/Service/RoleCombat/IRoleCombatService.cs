// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.RoleCombat;
using Ellen.Launcher.ViewModel.User;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.RoleCombat;

public interface IRoleCombatService
{
    ValueTask<ObservableCollection<RoleCombatView>> GetRoleCombatViewCollectionAsync(RoleCombatMetadataContext context, UserAndUid userAndUid);

    ValueTask RefreshRoleCombatAsync(RoleCombatMetadataContext context, UserAndUid userAndUid);
}