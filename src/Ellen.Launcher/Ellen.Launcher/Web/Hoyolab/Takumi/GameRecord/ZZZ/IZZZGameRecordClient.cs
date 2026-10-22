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
}
