// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Intrinsic;

[ExtendedEnum]
public enum MainQuestTag
{
    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagNone))]
    MAINQUEST_TAG_NONE,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagGuide))]
    MAINQUEST_TAG_GUIDE,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagActivity))]
    MAINQUEST_TAG_ACTIVITY,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagRankZeroWq))]
    MAINQUEST_TAG_RANK_ZERO_WQ,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagMainWq))]
    MAINQUEST_TAG_MAIN_WQ,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagReputation))]
    MAINQUEST_TAG_REPUTATION,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagNatlanTribal))]
    MAINQUEST_TAG_NATLAN_TRIBAL,

    MAINQUEST_TAG_ANECDOTE,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagFlowerland))]
    MAINQUEST_TAG_FLOWERLAND,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagThespia))]
    MAINQUEST_TAG_THESPIA,

    [LocalizationKey(nameof(SH.ModelIntrinsicMainQuestTagResidentActivity))]
    MAINQUEST_TAG_RESIDENT_ACTIVITY,
}
