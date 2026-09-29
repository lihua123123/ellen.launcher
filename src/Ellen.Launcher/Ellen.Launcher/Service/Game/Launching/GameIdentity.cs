// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.ViewModel.User;

namespace Ellen.Launcher.Service.Game.Launching;

public sealed class GameIdentity
{
    public UserAndUid? UserAndUid { get; init; }

    public GameAccount? GameAccount { get; init; }

    public static GameIdentity Create(UserAndUid? userAndUid, GameAccount? gameAccount)
    {
        return new()
        {
            UserAndUid = userAndUid,
            GameAccount = gameAccount,
        };
    }

    public static GameIdentity Create()
    {
        return new();
    }
}