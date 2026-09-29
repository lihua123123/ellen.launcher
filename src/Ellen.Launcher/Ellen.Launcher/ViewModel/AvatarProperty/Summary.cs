// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.UI.Xaml.Data;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public sealed class Summary
{
    public IAdvancedCollectionView<AvatarView> Avatars { get; set; } = default!;

    public static Summary Create(IAdvancedCollectionView<AvatarView> avatars)
    {
        return new()
        {
            Avatars = avatars,
        };
    }
}