// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Hoyolab.Takumi.Binding;
using Ellen.Launcher.Web.Hoyolab.Takumi.Event.BbsSignReward;
using Ellen.Launcher.Web.Response;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using BindingUser = Ellen.Launcher.ViewModel.User.User;

namespace Ellen.Launcher.Service.SignIn;

/// <summary>
/// 收集一个米游社账号下所有「可以签到」的游戏角色：
/// 原神取启动器数据库里的角色，星穹铁道 / 绝区零通过绑定接口查询（带短时缓存，避免启动时重复请求）。
/// </summary>
public interface IGameRoleCollectionService
{
    ValueTask<ImmutableArray<SignInGameRole>> GetRolesAsync(BindingUser user, CancellationToken token = default);
}

[Service(ServiceLifetime.Singleton, typeof(IGameRoleCollectionService))]
public sealed partial class GameRoleCollectionService : IGameRoleCollectionService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(5);

    private readonly BindingClient bindingClient;
    private readonly ConcurrentDictionary<Guid, CacheEntry> cache = [];

    [GeneratedConstructor]
    public partial GameRoleCollectionService(IServiceProvider serviceProvider);

    public async ValueTask<ImmutableArray<SignInGameRole>> GetRolesAsync(BindingUser user, CancellationToken token = default)
    {
        if (cache.TryGetValue(user.InnerId, out CacheEntry? entry) && entry is not null && DateTimeOffset.UtcNow - entry.Timestamp < CacheLifetime)
        {
            return entry.Roles;
        }

        ImmutableArray<SignInGameRole> roles = await CollectAsync(user, token).ConfigureAwait(false);
        if (!roles.IsEmpty)
        {
            cache[user.InnerId] = new(DateTimeOffset.UtcNow, roles);
        }

        return roles;
    }

    private async ValueTask<ImmutableArray<SignInGameRole>> CollectAsync(BindingUser user, CancellationToken token)
    {
        ImmutableArray<SignInGameRole>.Builder builder = ImmutableArray.CreateBuilder<SignInGameRole>();

        // 原神：启动器里已缓存的角色
        foreach (UserGameRole role in user.UserGameRoles.Source)
        {
            builder.Add(new()
            {
                Game = SignInGame.Genshin,
                Uid = role.GameUid,
                Region = role.Region.Value,
                GameBiz = user.IsOversea ? "hk4e_global" : "hk4e_cn",
                Nickname = role.Nickname,
                Level = role.Level,
            });
        }

        // 星穹铁道：绑定接口
        string starRailGameBiz = user.IsOversea ? "hkrpg_global" : "hkrpg_cn";
        Response<ListWrapper<GameRolePayload>> starRailResponse = await bindingClient
            .GetGameRolesByCookieAsync(user.Entity, starRailGameBiz, token)
            .ConfigureAwait(false);

        if (ResponseValidator.TryValidateWithoutUINotification(starRailResponse, out ListWrapper<GameRolePayload>? starRailRoles))
        {
            foreach (GameRolePayload role in starRailRoles.List)
            {
                builder.Add(new()
                {
                    Game = SignInGame.StarRail,
                    Uid = role.GameUid,
                    Region = role.Region,
                    GameBiz = string.IsNullOrEmpty(role.GameBiz) ? starRailGameBiz : role.GameBiz,
                    Nickname = role.Nickname,
                    Level = role.Level,
                });
            }
        }

        // 绝区零：绑定接口（国服/国际服通用）
        Response<ListWrapper<ZZZUserGameRole>> zenlessZoneZeroResponse = await bindingClient
            .GetZZZUserGameRolesAsync(user.Entity, token)
            .ConfigureAwait(false);

        if (ResponseValidator.TryValidateWithoutUINotification(zenlessZoneZeroResponse, out ListWrapper<ZZZUserGameRole>? zenlessZoneZeroRoles))
        {
            string zenlessZoneZeroGameBiz = user.IsOversea ? "nap_global" : "nap_cn";
            foreach (ZZZUserGameRole role in zenlessZoneZeroRoles.List)
            {
                builder.Add(new()
                {
                    Game = SignInGame.ZenlessZoneZero,
                    Uid = role.GameUid,
                    Region = role.Region,
                    GameBiz = string.IsNullOrEmpty(role.GameBiz) ? zenlessZoneZeroGameBiz : role.GameBiz,
                    Nickname = role.Nickname,
                    Level = role.Level,
                });
            }
        }

        return builder.ToImmutable();
    }

    private sealed record CacheEntry(DateTimeOffset Timestamp, ImmutableArray<SignInGameRole> Roles);
}
