// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.IO;

namespace Ellen.Launcher.Service.Git;

public interface IGitRepositoryService
{
    ValueTask<ValueResult<bool, ValueDirectory>> EnsureRepositoryAsync(string name);

    /// <summary>
    /// 在后台把本地仓库刷新到最新（自带节流与异常吞没，不阻塞调用方）。
    /// </summary>
    void EnsureRepositoryInBackground(string name);
}