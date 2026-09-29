// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity.Abstraction;

namespace Ellen.Launcher.Core.Database.Abstraction;

public interface ISelectable : IAppDbEntity
{
    bool IsSelected { get; set; }
}