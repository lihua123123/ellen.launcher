// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Core.ExceptionService;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data.Converter;

namespace Ellen.Launcher.Model.Metadata.Converter;

public sealed partial class EquipTypeIconConverter : ValueConverter<EquipType, Uri>
{
    public static Uri EquipTypeToIconUri(EquipType type)
    {
        string filename = type switch
        {
            EquipType.EQUIP_BRACER => "UI_Icon_RelicType1.png",
            EquipType.EQUIP_NECKLACE => "UI_Icon_RelicType2.png",
            EquipType.EQUIP_SHOES => "UI_Icon_RelicType3.png",
            EquipType.EQUIP_RING => "UI_Icon_RelicType4.png",
            EquipType.EQUIP_DRESS => "UI_Icon_RelicType5.png",
            _ => throw EllenException.NotSupported(),
        };

        return InstalledLocation.ToAbsoluteUri($"ms-appx:///Resource/Icon/{filename}");
    }

    public override Uri Convert(EquipType from)
    {
        return EquipTypeToIconUri(from);
    }
}
