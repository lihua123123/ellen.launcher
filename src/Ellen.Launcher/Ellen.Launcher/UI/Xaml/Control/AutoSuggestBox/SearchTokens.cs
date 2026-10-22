// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core;
using Ellen.Launcher.Model.Entity.Primitive.Converter;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Intrinsic.Frozen;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Monster;
using Ellen.Launcher.Model.Metadata.Weapon;
using Ellen.Launcher.ViewModel.AvatarProperty;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace Ellen.Launcher.UI.Xaml.Control.AutoSuggestBox;

public static class SearchTokens
{
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> ElementTokens = [.. IntrinsicFrozen.ElementNameValues.Select(static nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.ElementName, nv.Name, nv.Value, iconUri: ElementNameIconConverter.ElementNameToUri(nv.Name))))];
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> ItemQuality45Tokens = [.. IntrinsicFrozen.ItemQualityNameValues.Where(static nv => nv.Value >= QualityType.QUALITY_PURPLE).Select(nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.ItemQuality, nv.Name, (int)nv.Value, quality: QualityColorConverter.QualityToColor(nv.Value))))];
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> ItemQualityAllTokens = [.. IntrinsicFrozen.ItemQualityNameValues.Select(static nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.ItemQuality, nv.Name, (int)nv.Value, quality: QualityColorConverter.QualityToColor(nv.Value))))];
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> WeaponTypeTokens = [.. IntrinsicFrozen.WeaponTypeNameValues.Select(static nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.WeaponType, nv.Name, (int)nv.Value, iconUri: WeaponTypeIconConverter.WeaponTypeToIconUri(nv.Value))))];
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> AssociationTypeTokens = [.. IntrinsicFrozen.AssociationTypeNameValues.Select(static nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.AssociationType, nv.Name, (int)nv.Value, iconUri: AssociationTypeIconConverter.AssociationTypeToIconUri(nv.Value))))];
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> BodyTypeTokens = [.. IntrinsicFrozen.BodyTypeNameValues.Select(static nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.BodyType, nv.Name, (int)nv.Value)))];
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> FightPropertyTokens = [.. IntrinsicFrozen.FightPropertyNameValues.Select(static nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.FightProperty, nv.Name, (int)nv.Value)))];
    private static readonly ImmutableArray<KeyValuePair<string, SearchToken>> CultivateTypeTokens = [.. IntrinsicFrozen.CultivateTypeNameValues.Select(static nv => KeyValuePair.Create(nv.Name, new SearchToken(SearchTokenKind.CultivateType, nv.Name, (int)nv.Value, packageIconUri: CultivateTypeIconConverter.CultivateTypeToIconUri(nv.Value))))];

    private static readonly LazySlim<FrozenDictionary<string, SearchToken>> AvatarPropertyTokens = new(() => WinRTAdaptive.ToFrozenDictionary(
    [.. ElementTokens, .. ItemQuality45Tokens, .. WeaponTypeTokens]));

    private static readonly LazySlim<FrozenDictionary<string, SearchToken>> CultivationTokens = new(() => WinRTAdaptive.ToFrozenDictionary(
    [.. CultivateTypeTokens, .. ElementTokens, .. ItemQualityAllTokens, .. WeaponTypeTokens]));

    private static readonly LazySlim<FrozenDictionary<string, SearchToken>> ZZZAvatarPropertyTokens = new(() =>
    {
        ZZZProfessionType[] professions = [.. Enum.GetValues<ZZZProfessionType>().Where(static profession => profession is not ZZZProfessionType.None)];
        ZZZElementType[] elements = [.. Enum.GetValues<ZZZElementType>().Where(static element => element is not ZZZElementType.None)];

        return WinRTAdaptive.ToFrozenDictionary(
        [
            .. professions.Select((profession, index) =>
            {
                string name = profession.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture) ?? profession.ToString();
                return KeyValuePair.Create(name, new SearchToken(SearchTokenKind.ZZZProfession, name, index, iconUri: ZZZFilterIcons.ForProfession(profession)));
            }),
            .. elements.Select((element, index) =>
            {
                string name = element.GetLocalizedDescription(SH.ResourceManager, CultureInfo.CurrentCulture) ?? element.ToString();
                return KeyValuePair.Create(name, new SearchToken(SearchTokenKind.ZZZElement, name, index, iconUri: ZZZFilterIcons.ForElement(element)));
            }),
            // 与原神一致：四星 / 五星 / 限定五星。
            .. ItemQuality45Tokens,
        ]);
    });

    public static FrozenDictionary<string, SearchToken> GetForAvatarProperty()
    {
        return AvatarPropertyTokens.Value;
    }

    public static FrozenDictionary<string, SearchToken> GetForZZZAvatarProperty()
    {
        return ZZZAvatarPropertyTokens.Value;
    }

    public static FrozenDictionary<string, SearchToken> GetForCultivation()
    {
        return CultivationTokens.Value;
    }

    public static FrozenDictionary<string, SearchToken> GetForWikiAvatar(ImmutableArray<Avatar> array)
    {
        return WinRTAdaptive.ToFrozenDictionary(
        [
            .. array.Select((avatar, index) => KeyValuePair.Create(avatar.Name, new SearchToken(SearchTokenKind.Avatar, avatar.Name, index, sideIconUri: AvatarSideIconConverter.IconNameToUri(avatar.SideIcon)))),
            .. AssociationTypeTokens,
            .. BodyTypeTokens,
            .. ElementTokens,
            .. ItemQuality45Tokens,
            .. WeaponTypeTokens,
        ]);
    }

    public static FrozenDictionary<string, SearchToken> GetForWikiWeapon(ImmutableArray<Weapon> array)
    {
        return WinRTAdaptive.ToFrozenDictionary(
        [
            .. array.Select((weapon, index) => KeyValuePair.Create(weapon.Name, new SearchToken(SearchTokenKind.Weapon, weapon.Name, index, sideIconUri: EquipIconConverter.IconNameToUri(weapon.Icon)))),
            .. FightPropertyTokens,
            .. ItemQualityAllTokens,
            .. WeaponTypeTokens,
        ]);
    }
    public static FrozenDictionary<string, SearchToken> GetForWikiMonster(ImmutableArray<Monster> array)
    {
        return WinRTAdaptive.ToFrozenDictionary(
        [
            .. array.Select((monster, index) => KeyValuePair.Create(monster.Name!, new SearchToken(SearchTokenKind.Monster, monster.Name!, index, sideIconUri: MonsterIconConverter.IconNameToUri(monster.Icon)))),
        ]);
    }

}