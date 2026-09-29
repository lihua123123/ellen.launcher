// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Property;
using Ellen.Launcher.Service.Game.PathAbstraction;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Game.FileSystem;

public interface IRestrictedGamePathAccess
{
    IObservableProperty<GamePathEntry?> GamePathEntry { get; }

    IObservableProperty<ImmutableArray<GamePathEntry>> GamePathEntries { get; }

    AsyncReaderWriterLock GamePathLock { get; }
}