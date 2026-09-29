// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Threading;
using Ellen.Launcher.ViewModel.GachaLog;

namespace Ellen.Launcher.Service.GachaLog.Factory;

/// <summary>
/// 抽卡结果预测占位实现（云端预测功能已移除）
/// </summary>
internal sealed class PullPrediction
{
    public PullPrediction(TypedWishSummary summary, TypedWishSummaryBuilderContext context)
    {
    }

    public ValueTask PredictAsync(AsyncBarrier barrier)
    {
        return ValueTask.CompletedTask;
    }
}
