// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;

public interface ISignInClient
{
    /// <summary>
    /// 查询某个游戏角色今天的签到状态。
    /// </summary>
    ValueTask<Response<SignInRewardInfo>> GetInfoAsync(Model.Entity.User user, SignInGameRole role, CancellationToken token = default);

    /// <summary>
    /// 查询本月的签到奖励列表（用于展示签到获得的物品图标）。
    /// </summary>
    ValueTask<Response<Reward>> GetRewardAsync(Model.Entity.User user, SignInGameRole role, CancellationToken token = default);

    /// <summary>
    /// 为某个游戏角色执行签到。
    /// </summary>
    ValueTask<Response<SignInResult>> SignAsync(Model.Entity.User user, SignInGameRole role, CancellationToken token = default);
}
