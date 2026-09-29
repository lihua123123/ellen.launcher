// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.ViewModel.HardChallenge;
using Ellen.Launcher.ViewModel.User;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Ellen.Launcher.Service.HardChallenge;

public interface IHardChallengeService
{
    ValueTask<ObservableCollection<HardChallengeView>> GetHardChallengeViewCollectionAsync(HardChallengeMetadataContext context, UserAndUid userAndUid);

    ValueTask<ImmutableArray<AvatarView>> GetBlingAvatarsAsync(HardChallengeMetadataContext context, UserAndUid userAndUid);

    ValueTask RefreshHardChallengeAsync(HardChallengeMetadataContext context, UserAndUid userAndUid);
}