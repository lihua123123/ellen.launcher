// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.UI.Xaml.Data.Converter;
using System.Collections.Frozen;

namespace Ellen.Launcher.Model.Entity.Primitive.Converter;

public sealed partial class CultivateTypeIconConverter : ValueConverter<CultivateType, Uri>
{
    private static readonly FrozenDictionary<string, CultivateType> LocalizedNameToCultivateType = WinRTAdaptive.ToFrozenDictionary(
    [
        KeyValuePair.Create(SH.ModelEntityPrimitiveCultivateTypeAvatarAndSkill, CultivateType.AvatarAndSkill),
        KeyValuePair.Create(SH.ModelEntityPrimitiveCultivateTypeWeapon, CultivateType.Weapon),
        KeyValuePair.Create(SH.ModelEntityPrimitiveCultivateTypeFurniture, CultivateType.Furniture),
    ]);

    public static Uri CultivateTypeNameToIconUri(string cultivateTypeName)
    {
        return CultivateTypeToIconUri(LocalizedNameToCultivateType.GetValueOrDefault(cultivateTypeName));
    }

    public static Uri CultivateTypeToIconUri(CultivateType type)
    {
        string filename = type switch
        {
            CultivateType.AvatarAndSkill => "UI_BtnIcon_PlayerGirl.png",
            CultivateType.Weapon => "UI_BagTabIcon_Weapon.png",
            CultivateType.Furniture => "UI_BtnIcon_Homeworld.png",
            _ => throw EllenException.NotSupported(),
        };

        return InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/Icon/{filename}");
    }

    public override Uri Convert(CultivateType from)
    {
        return CultivateTypeToIconUri(from);
    }
}