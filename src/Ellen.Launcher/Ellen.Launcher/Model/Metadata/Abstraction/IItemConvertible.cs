// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Model.Metadata.Abstraction;

public interface IItemConvertible
{
    TItem ToItem<TItem>()
        where TItem : Model.Item, new();
}