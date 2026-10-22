// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;

/// <summary>
/// 游戏签到请求体。区服与 UID 都用原始字符串，兼容原神 / 星穹铁道 / 绝区零。
/// </summary>
public sealed class SignInGameData
{
    public SignInGameData(string activityId, SignInGameRole role)
    {
        ActivityId = activityId;
        Region = role.Region;
        Uid = role.Uid;
    }

    [JsonPropertyName("act_id")]
    public string ActivityId { get; }

    [JsonPropertyName("region")]
    public string Region { get; }

    [JsonPropertyName("uid")]
    public string Uid { get; }
}
