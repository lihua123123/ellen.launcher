// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Intrinsic;

namespace Ellen.Launcher.Model.Metadata.Abstraction;

public interface INameQualityAccess : INameAccess
{
    QualityType Quality { get; }
}