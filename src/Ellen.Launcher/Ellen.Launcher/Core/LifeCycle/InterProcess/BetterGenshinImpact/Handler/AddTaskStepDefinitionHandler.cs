// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.LifeCycle.InterProcess.BetterGenshinImpact.Task;

namespace Ellen.Launcher.Core.LifeCycle.InterProcess.BetterGenshinImpact.Handler;

[Service(ServiceLifetime.Singleton, typeof(IPipeRequestHandler))]
public sealed class AddTaskStepDefinitionHandler : PipeRequestHandler<AddAutomationTaskStepDefinition>
{
    private readonly IAutomationTaskService automationTaskService;

    public AddTaskStepDefinitionHandler(IAutomationTaskService automationTaskService)
        : base(PipeRequestKind.AddTaskStepDefinition)
    {
        this.automationTaskService = automationTaskService;
    }

    protected override ValueTask<PipeResponse> HandleAsync(AddAutomationTaskStepDefinition add)
    {
        return ValueTask.FromResult<PipeResponse>(automationTaskService.AddTaskStepDefinition(add.Id, add.Description));
    }
}
