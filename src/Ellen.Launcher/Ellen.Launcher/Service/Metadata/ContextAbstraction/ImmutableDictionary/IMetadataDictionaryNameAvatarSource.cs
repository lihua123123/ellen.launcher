// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata.ContextAbstraction.ImmutableDictionary;

public interface IMetadataDictionaryNameAvatarSource
{
    ImmutableDictionary<string, Model.Metadata.Avatar.Avatar> NameAvatarMap { get; set; }
}