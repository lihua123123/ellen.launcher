// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.BetterGenshinImpact.Cultivation;

public interface IAutomationCultivationService
{
    ValueTask<AutomationCultivationProject?> GetCurrentProjectAsync(CancellationToken token = default);
}