// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Primitive;

namespace Ellen.Launcher.ViewModel.Game;

public sealed class GameAccountFilter
{
    private readonly SchemeType? type;

    private GameAccountFilter(SchemeType? type)
    {
        this.type = type;
    }

    public static Predicate<GameAccount> Create(SchemeType? type)
    {
        return new GameAccountFilter(type).Filter;
    }

    public bool Filter(GameAccount? item)
    {
        if (type is null)
        {
            return true;
        }

        return item is not null && item.Type == type;
    }
}