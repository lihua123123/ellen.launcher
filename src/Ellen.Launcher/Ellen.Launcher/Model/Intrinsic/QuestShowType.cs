// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Intrinsic;

[ExtendedEnum]
public enum QuestShowType
{
    [LocalizationKey(nameof(SH.ModelIntrinsicQuestShowTypeShow))]
    QUEST_SHOW,

    [LocalizationKey(nameof(SH.ModelIntrinsicQuestShowTypeHidden))]
    QUEST_HIDDEN,
}
