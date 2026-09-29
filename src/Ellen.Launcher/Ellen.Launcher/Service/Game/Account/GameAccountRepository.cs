// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Abstraction;

namespace Ellen.Launcher.Service.Game.Account;

[Service(ServiceLifetime.Singleton, typeof(IGameAccountRepository))]
public sealed partial class GameAccountRepository : IGameAccountRepository
{
    [GeneratedConstructor]
    public partial GameAccountRepository(IServiceProvider serviceProvider);

    public partial IServiceProvider ServiceProvider { get; }

    public ObservableReorderableDbCollection<GameAccount> GetGameAccountCollection()
    {
        return this.Query(query => query.ToObservableReorderableDbCollection(ServiceProvider));
    }

    public void AddGameAccount(GameAccount gameAccount)
    {
        this.Add(gameAccount);
    }

    public void UpdateGameAccount(GameAccount gameAccount)
    {
        this.Update(gameAccount);
    }

    public void RemoveGameAccountById(Guid id)
    {
        this.DeleteByInnerId(id);
    }
}