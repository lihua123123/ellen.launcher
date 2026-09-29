// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Game.Island;

public interface IEmbeddedYaeGate
{
    /// <summary>
    /// 确保用户已经确认过使用 Embedded Yae 注入的风险。
    /// 首次调用会弹出确认框，用户确认后持久化开启，之后的调用直接返回 true。
    /// </summary>
    /// <returns>是否允许继续使用 Embedded Yae。</returns>
    ValueTask<bool> EnsureConfirmedAsync();
}
