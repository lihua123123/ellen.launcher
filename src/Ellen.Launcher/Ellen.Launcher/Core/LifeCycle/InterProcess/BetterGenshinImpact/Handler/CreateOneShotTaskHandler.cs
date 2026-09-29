// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.LifeCycle.InterProcess.BetterGenshinImpact.Task;

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.BetterGenshinImpact.Handler;

[Service(ServiceLifetime.Singleton, typeof(IPipeRequestHandler))]
public sealed class CreateOneShotTaskHandler : PipeRequestHandler<AutomationTaskDefinition>
{
    private readonly IAutomationTaskService automationTaskService;

    public CreateOneShotTaskHandler(IAutomationTaskService automationTaskService)
        : base(PipeRequestKind.CreateOneShotTask)
    {
        this.automationTaskService = automationTaskService;
    }

    protected override ValueTask<PipeResponse> HandleAsync(AutomationTaskDefinition definition)
    {
        return ValueTask.FromResult<PipeResponse>(automationTaskService.CreateOneShotTask(definition));
    }
}
