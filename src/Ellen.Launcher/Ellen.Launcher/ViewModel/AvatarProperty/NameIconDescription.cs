// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public abstract class NameIconDescription : NameDescription, INameIcon<Uri>
{
    public Uri Icon { get; set; } = default!;
}