// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.


// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.ActCalendar;

public class ActOther : Act
{
    [JsonPropertyName("other_act_detail")]
    public ActOtherDetail? OtherActDetail { get; init; }
}