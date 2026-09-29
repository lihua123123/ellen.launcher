// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Endgame;
using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Model.Metadata.Avatar;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.HardChallenge;

public sealed class HardChallengeMetadataContext : IMetadataContext,
    IMetadataDictionaryIdHardChallengeScheduleSource,
    IMetadataDictionaryIdAvatarWithPlayersSource
{
    public ImmutableDictionary<HardChallengeScheduleId, HardChallengeSchedule> IdHardChallengeScheduleMap { get; set; } = default!;

    public ImmutableDictionary<AvatarId, Avatar> IdAvatarMap { get; set; } = default!;

    /// <summary>
    /// 内置幽境 Boss 攻略，用于给挑战记录卡片补上血量 / 机制 / 介绍背景。
    /// </summary>
    public ImmutableArray<HardChallengeBossGuidePeriod> BossGuidePeriods { get; set; } = [];
}