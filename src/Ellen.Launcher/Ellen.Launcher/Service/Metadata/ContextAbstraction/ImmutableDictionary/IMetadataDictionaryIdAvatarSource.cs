// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;

public interface IMetadataDictionaryIdAvatarSource
{
    ImmutableDictionary<AvatarId, Model.Metadata.Avatar.Avatar> IdAvatarMap { get; set; }
}