// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.ViewModel.AvatarProperty;

[ExtendedEnum]
public enum AvatarPropertySortDescriptionKind
{
    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindDefault))]
    Default,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindLevelNumber))]
    LevelNumber,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindQuality))]
    Quality,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindActivatedConstellationCount))]
    ActivatedConstellationCount,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindFetterLevel))]
    FetterLevel,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindMaxHp))]
    MaxHp,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindCurAttack))]
    CurAttack,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindCurDefense))]
    CurDefense,

    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindElementMastery))]
    ElementMastery,

    /// <summary>
    /// 绝区零专用：按影画解锁层数排序（字段与命之座一致，显示名不同）。
    /// </summary>
    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindZZZActivatedConstellationCount))]
    ZZZActivatedConstellationCount,

    /// <summary>
    /// 绝区零专用：按异常精通排序（字段与元素精通一致，显示名不同）。
    /// </summary>
    [LocalizationKey(nameof(SH.ViewModelAvatarPropertySortDescriptionKindZZZElementMastery))]
    ZZZElementMastery,
}