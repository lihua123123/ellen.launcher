// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Model.Metadata;

public sealed class MainQuest
{
    public required MainQuestId Id { get; init; }

    public required QuestType Type { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? UnlockDescription { get; init; }

    public required ChapterId ChapterId { get; init; }

    public required uint SortWeight { get; init; }

    public required uint RecommendLevel { get; init; }

    public required uint ActivityId { get; init; }

    public required MainQuestTag MainQuestTag { get; init; }

    public required QuestShowType ShowType { get; init; }

    public required bool Repeatable { get; init; }

    public required uint Series { get; init; }

    public required uint TaskId { get; init; }

    public required ImmutableArray<IdCount> RewardList { get; init; }
}
