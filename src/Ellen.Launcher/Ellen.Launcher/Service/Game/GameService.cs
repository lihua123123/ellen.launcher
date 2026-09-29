// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.Service.Game.Account;
using Ellen.Launcher.Service.Game.Configuration;
using Ellen.Launcher.Service.Game.PathAbstraction;
using Ellen.Launcher.UI.Xaml.Data;

namespace Ellen.Launcher.Service.Game;

[Service(ServiceLifetime.Singleton, typeof(IGameService))]
public sealed partial class GameService : IGameService
{
    private readonly IGameInRegistryAccountService gameInRegistryAccountService;
    private readonly IGameChannelOptionsService gameChannelOptionsService;
    private readonly IGamePathService gamePathService;

    [GeneratedConstructor]
    public partial GameService(IServiceProvider serviceProvider);

    public ValueTask<IAdvancedCollectionView<GameAccount>> GetGameAccountCollectionAsync()
    {
        return gameInRegistryAccountService.GetGameAccountCollectionAsync();
    }

    public ValueTask<ValueResult<bool, string>> GetGamePathAsync()
    {
        return gamePathService.SilentLocateGamePathAsync();
    }

    public ChannelOptions GetChannelOptions()
    {
        return gameChannelOptionsService.GetChannelOptions();
    }

    public ValueTask<GameAccount?> DetectGameAccountAsync(SchemeType scheme, Func<string, Task<ValueResult<bool, string?>>> providerNameCallback)
    {
        return gameInRegistryAccountService.DetectCurrentGameAccountAsync(scheme, providerNameCallback);
    }

    public GameAccount? DetectCurrentGameAccount(SchemeType scheme)
    {
        return gameInRegistryAccountService.DetectCurrentGameAccount(scheme);
    }

    public ValueTask ModifyGameAccountAsync(GameAccount gameAccount, Func<string, Task<ValueResult<bool, string?>>> providerNameCallback)
    {
        return gameInRegistryAccountService.ModifyGameAccountAsync(gameAccount, providerNameCallback);
    }

    public ValueTask RemoveGameAccountAsync(GameAccount gameAccount)
    {
        return gameInRegistryAccountService.RemoveGameAccountAsync(gameAccount);
    }
}