// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.Avatar;
using System.Globalization;

namespace Ellen.Launcher.ViewModel.RoleCombat;

public sealed class AvatarDamage : AvatarView
{
    public AvatarDamage(string value, Avatar metaAvatar)
        : base(metaAvatar)
    {
        int.TryParse(value, CultureInfo.InvariantCulture, out int result);
        Value = result;
    }

    public int Value { get; }
}