// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Service.Game.Configuration;
using Ellen.Launcher.UI.Xaml.Data;

namespace Ellen.Launcher.Service.Game;

public interface IGameService
{
    ValueTask<GameAccount?> DetectGameAccountAsync(SchemeType scheme, Func<string, Task<ValueResult<bool, string?>>> providerNameCallback);

    ValueTask<ValueResult<bool, string>> GetGamePathAsync();

    ChannelOptions GetChannelOptions();

    ValueTask ModifyGameAccountAsync(GameAccount gameAccount, Func<string, Task<ValueResult<bool, string?>>> providerNameCallback);

    ValueTask RemoveGameAccountAsync(GameAccount gameAccount);

    GameAccount? DetectCurrentGameAccount(SchemeType scheme);

    ValueTask<IAdvancedCollectionView<GameAccount>> GetGameAccountCollectionAsync();
}