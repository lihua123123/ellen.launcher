// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

public abstract class EquipView : NameIconDescription
{
    public string Level { get; set; } = default!;

    public QualityType Quality { get; set; }

    public NameValue<string>? MainProperty { get; set; }

    public EquipType EquipType { get; set; }
}
