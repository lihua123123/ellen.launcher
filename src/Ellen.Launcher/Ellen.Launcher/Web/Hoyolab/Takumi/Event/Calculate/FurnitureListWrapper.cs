// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Web.Response;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

public sealed class FurnitureListWrapper : ListWrapper<Item>
{
    [JsonPropertyName("not_calc_list")]
    public List<Item>? NotCalculateList { get; set; }
}