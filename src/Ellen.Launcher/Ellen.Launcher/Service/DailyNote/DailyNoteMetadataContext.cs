// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata;
using Ellen.Launcher.Service.Metadata.ContextAbstraction;
using Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableArray;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.DailyNote;

public class DailyNoteMetadataContext : IMetadataContext,
    IMetadataArrayChapterSource
{
    public ImmutableArray<Chapter> Chapters { get; set; } = default!;
}
