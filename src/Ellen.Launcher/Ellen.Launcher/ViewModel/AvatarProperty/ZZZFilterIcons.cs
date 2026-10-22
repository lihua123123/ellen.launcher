// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.ViewModel.AvatarProperty;

/// <summary>
/// ZZZ 职业/属性搜索 token 图标。
/// 职业图标来源 wikiwiki.jp（ゼンレスゾーンゼロ Wiki），属性图标来源 wikiwiki.jp；
/// 职业图标原图为黑色，构建资源时转为白色，配合 token 的深色底保证深浅主题都能看清。
/// </summary>
internal static class ZZZFilterIcons
{
    private const string Prefix = "ms-appx:///Resource/ZZZIcon/";

    public static Uri? ForProfession(ZZZProfessionType profession)
    {
        return profession switch
        {
            ZZZProfessionType.Attack => new($"{Prefix}Profession_Attack.png"),
            ZZZProfessionType.Stun => new($"{Prefix}Profession_Stun.png"),
            ZZZProfessionType.Anomaly => new($"{Prefix}Profession_Anomaly.png"),
            ZZZProfessionType.Support => new($"{Prefix}Profession_Support.png"),
            ZZZProfessionType.Defense => new($"{Prefix}Profession_Defense.png"),
            ZZZProfessionType.Rupture => new($"{Prefix}Profession_Rupture.png"),
            ZZZProfessionType.Vanguard => new($"{Prefix}Profession_Vanguard.png"),
            _ => null,
        };
    }

    public static Uri? ForElement(ZZZElementType element)
    {
        return element switch
        {
            ZZZElementType.Physical => new($"{Prefix}Element_Physical.png"),
            ZZZElementType.Fire => new($"{Prefix}Element_Fire.png"),
            ZZZElementType.Ice => new($"{Prefix}Element_Ice.png"),
            ZZZElementType.Electric => new($"{Prefix}Element_Electric.png"),
            ZZZElementType.Wind => new($"{Prefix}Element_Wind.png"),
            ZZZElementType.Ether => new($"{Prefix}Element_Ether.png"),
            ZZZElementType.Frost => new($"{Prefix}Element_Frost.png"),
            ZZZElementType.Ink => new($"{Prefix}Element_Ink.png"),
            ZZZElementType.Rime => new($"{Prefix}Element_Rime.png"),
            ZZZElementType.Lumen => new($"{Prefix}Element_Lumen.png"),
            _ => null,
        };
    }
}
