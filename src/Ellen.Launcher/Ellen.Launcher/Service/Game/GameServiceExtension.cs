// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Entity;
using Ellen.Launcher.Service.Game.Scheme;

namespace Ellen.Launcher.Service.Game;

public static class GameServiceExtension
{
    extension(IGameService gameService)
    {
        public GameAccount? DetectCurrentGameAccountNoThrow(LaunchScheme scheme)
        {
            try
            {
                return gameService.DetectCurrentGameAccount(scheme.SchemeType);
            }
            catch
            {
                return default;
            }
        }

        public ValueTask<GameAccount?> DetectGameAccountAsync(LaunchScheme scheme, Func<string, Task<ValueResult<bool, string?>>> providerNameCallback)
        {
            return gameService.DetectGameAccountAsync(scheme.SchemeType, providerNameCallback);
        }
    }
}