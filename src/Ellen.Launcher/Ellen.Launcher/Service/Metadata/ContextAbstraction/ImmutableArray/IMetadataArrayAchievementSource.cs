// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;

public interface IMetadataArrayAchievementSource
{
    ImmutableArray<Model.Metadata.Achievement.Achievement> Achievements { get; set; }
}