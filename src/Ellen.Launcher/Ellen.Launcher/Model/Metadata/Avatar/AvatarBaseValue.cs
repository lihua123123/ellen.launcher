// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Model.Metadata.Avatar;

public sealed class AvatarBaseValue : BaseValue
{
    public override float GetValue(FightProperty fightProperty)
    {
        return fightProperty switch
        {
            FightProperty.FIGHT_PROP_CRITICAL => 0.05F,
            FightProperty.FIGHT_PROP_CRITICAL_HURT => 0.5F,
            _ => base.GetValue(fightProperty),
        };
    }
}