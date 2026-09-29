// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data.Converter;
using Ellen.Launcher.Web.Endpoint.Upstream;

namespace Ellen.Launcher.Model.Metadata.Converter;

public sealed partial class CookFoodTypeIconConverter : ValueConverter<CookFoodType, Uri>
{
    public static Uri CookFoodTypeToIconUri(CookFoodType type)
    {
        string filename = type switch
        {
            CookFoodType.COOK_FOOD_ATTACK => "UI_Talent_Cook_Attack.png",
            CookFoodType.COOK_FOOD_DEFENSE => "UI_Talent_Cook_Defense.png",
            CookFoodType.COOK_FOOD_FUNCTION => "UI_Talent_Cook_ExtraFailedItem.png",
            CookFoodType.COOK_FOOD_HEAL => "UI_Talent_Cook_Heal.png",
            _ => throw EllenException.NotSupported(),
        };

        return StaticResourcesEndpoints.StaticRaw("Talent", filename).ToUri();
    }

    public override Uri Convert(CookFoodType from)
    {
        return CookFoodTypeToIconUri(from);
    }
}
