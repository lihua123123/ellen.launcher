// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;

namespace Ellen.Launcher.Service.Game.Account;

public interface IGameAccountRepository : IRepository<GameAccount>
{
    void AddGameAccount(GameAccount gameAccount);

    void RemoveGameAccountById(Guid id);

    ObservableReorderableDbCollection<GameAccount> GetGameAccountCollection();

    void UpdateGameAccount(GameAccount gameAccount);
}