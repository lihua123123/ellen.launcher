// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Cultivation.Consumption;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

namespace Ellen.Launcher.UI.Xaml.View.Dialog;

public sealed class CultivatePromotionDeltaOptions
{
    public CultivatePromotionDeltaOptions(AvatarPromotionDelta delta, ConsumptionSaveStrategyKind strategy)
    {
        delta.AvatarLevelTarget = delta.AvatarLevelTarget switch
        {
            >= 100 => 100,
            >= 95 => 95,
            _ => delta.AvatarLevelTarget
        };

        Delta = delta;
        Strategy = strategy;
    }

    public AvatarPromotionDelta Delta { get; }

    public ConsumptionSaveStrategyKind Strategy { get; }
}