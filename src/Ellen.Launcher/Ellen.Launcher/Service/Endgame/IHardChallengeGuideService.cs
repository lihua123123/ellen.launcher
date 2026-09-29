// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Endgame;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Endgame;

/// <summary>
/// 提供随应用发布的幽境 Boss 攻略数据（离线可用，无需登录）。
/// </summary>
public interface IHardChallengeGuideService
{
    ValueTask<ImmutableArray<HardChallengeBossGuidePeriod>> GetPeriodsAsync();
}
