// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.UI.Xaml.Data;

namespace Ellen.Launcher.Service.Game.Account;

public interface IGameInRegistryAccountService
{
    GameAccount? DetectCurrentGameAccount(SchemeType schemeType);

    ValueTask<GameAccount?> DetectCurrentGameAccountAsync(SchemeType schemeType, Func<string, Task<ValueResult<bool, string?>>> providerNameCallback);

    ValueTask<IAdvancedCollectionView<GameAccount>> GetGameAccountCollectionAsync();

    ValueTask ModifyGameAccountAsync(GameAccount gameAccount, Func<string, Task<ValueResult<bool, string?>>> providerNameCallback);

    ValueTask RemoveGameAccountAsync(GameAccount gameAccount);

    bool SetCurrentGameAccount(GameAccount account);
}