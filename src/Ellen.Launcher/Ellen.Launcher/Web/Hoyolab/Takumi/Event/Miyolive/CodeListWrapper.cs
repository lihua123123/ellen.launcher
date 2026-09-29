// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Miyolive;

public sealed class CodeListWrapper
{
    [JsonPropertyName("code_list")]
    public required ImmutableArray<CodeWrapper> CodeList { get; init; }
}