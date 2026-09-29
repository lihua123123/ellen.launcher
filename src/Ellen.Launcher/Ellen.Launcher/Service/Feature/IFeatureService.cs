// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Service.Yae.Achievement;

namespace Ellen.Launcher.Service.Feature;

public interface IFeatureService
{
    ValueTask<AchievementFieldId?> GetAchievementFieldIdAsync(string tag);
}