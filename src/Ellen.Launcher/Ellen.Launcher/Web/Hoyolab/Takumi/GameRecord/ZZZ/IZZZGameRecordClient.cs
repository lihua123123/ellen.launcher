// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ZZZ;

public interface IZZZGameRecordClient
{
    ValueTask<Response<ZZZAvatarListWrapper<ZZZAvatarBasic>>> GetZZZAgentsAsync(
        User user,
        string uid,
        string server,
        CancellationToken token = default);

    ValueTask<Response<JsonElement>> GetZZZAgentInfoAsync(
        User user,
        string uid,
        string server,
        int agentId,
        CancellationToken token = default);

    /// <summary>
    /// 绝区零实时便笺。需要已注册的 x-rpc-device_fp，缺失会返回 10041。
    /// </summary>
    ValueTask<Response<Zenless.ZenlessDailyNote>> GetZZZNoteAsync(
        User user,
        string uid,
        string server,
        CancellationToken token = default);
}
