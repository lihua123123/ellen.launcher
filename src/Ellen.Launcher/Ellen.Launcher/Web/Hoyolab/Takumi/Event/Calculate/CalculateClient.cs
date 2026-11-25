// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.DependencyInjection.Annotation.HttpClient;
using Ellen.Launcher.ViewModel.User;
using Ellen.Launcher.Web.Endpoint.Hoyolab;
using Ellen.Launcher.Web.Request.Builder;
using Ellen.Launcher.Web.Request.Builder.Abstraction;
using Ellen.Launcher.Web.Response;
using System.Collections.Immutable;
using System.Net.Http;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class CalculateClient
{
    private const int BatchComputeRejected = -500001;

    private readonly IHttpRequestMessageBuilderFactory httpRequestMessageBuilderFactory;
    private readonly IApiEndpointsFactory apiEndpointsFactory;
    private readonly IServiceProvider serviceProvider;

    private readonly HttpClient httpClient;

    [GeneratedConstructor]
    public partial CalculateClient(IServiceProvider serviceProvider, HttpClient httpClient);

    public async ValueTask<Response<BatchConsumption>> BatchComputeAsync(UserAndUid userAndUid, AvatarPromotionDelta delta, bool syncInventory = false, CancellationToken token = default)
    {
        return await BatchComputeAsync(userAndUid, [delta], syncInventory, token).ConfigureAwait(false);
    }

    public async ValueTask<Response<BatchConsumption>> BatchComputeAsync(UserAndUid userAndUid, ImmutableArray<AvatarPromotionDelta> deltas, bool syncInventory = false, CancellationToken token = default)
    {
        if (deltas.IsDefaultOrEmpty)
        {
            return new Response<BatchConsumption>(-1, SH.ServiceInventoryCalculatorNoDeltas, default);
        }

        // 与 Snap.Hutao.Remastered 完全一致：一次发送完整 delta 数组，不拆批、不合并响应。
        Response<BatchConsumption> response = await SendBatchComputeAsync(userAndUid, deltas, syncInventory, token).ConfigureAwait(false);
        if (!syncInventory || response.ReturnCode != BatchComputeRejected)
        {
            return response;
        }

        /* 7.2.0.7 的边界探针（174 条 delta）已经证明 -500001 不是网络、不是体积：
           - 同一批 174 条「整体去掉 skill_list」返回 0，响应约 7.5 MB；
           - 前缀 68 条（6.37 MB）本来就能过，所以上限与体积无关；
           - 单条旅行者带 skill_list 返回 -500001，去掉后返回 0。
           所以这里只做一次「整套省略 skill_list」的完整重试：
           响应仍是整份账号结果，只是这一轮不含天赋书，绝不拆批、不做跨响应合并。 */
        ImmutableArray<AvatarPromotionDelta> withoutSkills = deltas.SelectAsArray(CloneWithoutSkills);
        Response<BatchConsumption> retry = await SendBatchComputeAsync(userAndUid, withoutSkills, true, token).ConfigureAwait(false);
        if (retry.ReturnCode == 0 && retry.Data is not null)
        {
            return retry;
        }

        return response;
    }

    private static AvatarPromotionDelta CloneWithoutSkills(AvatarPromotionDelta source)
    {
        return new()
        {
            AvatarId = source.AvatarId,
            AvatarLevelCurrent = source.AvatarLevelCurrent,
            AvatarLevelTarget = source.AvatarLevelTarget,
            ElementAttrId = source.ElementAttrId,
            AvatarPromoteLevel = source.AvatarPromoteLevel,
            FromUserSync = source.FromUserSync,
            Weapon = source.Weapon,
        };
    }

    private async ValueTask<Response<BatchConsumption>> SendBatchComputeAsync(
        UserAndUid userAndUid,
        ImmutableArray<AvatarPromotionDelta> deltas,
        bool syncInventory,
        CancellationToken token)
    {
        BatchConsumptionData data = new()
        {
            Items = deltas,
            Region = syncInventory ? userAndUid.Uid.Region : default!,
            Uid = syncInventory ? userAndUid.Uid.ToString() : default!,
        };

        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(userAndUid.IsOversea);

        /* 请求形状与 Snap.Hutao.Remastered 保持一致：不额外塞 lang，也不自己补 x-rpc-* 头。
           之前这里加过 x-rpc-page / x-rpc-cal_type / 自定义 device_id 等，并改用 act.mihoyo.com
           作 Referer，反而让服务端按「网页会话」校验，同步一直失败。 */
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CalculateBatchCompute())
            .SetUserCookieAndFpHeader(userAndUid.User, CookieType.Cookie)
            .SetReferer(userAndUid.IsOversea ? apiEndpoints.ActHoyolabReferer() : apiEndpoints.WebStaticReferer())
            .PostJson(data);

        Response<BatchConsumption>? resp = await builder
            .SendAsync<Response<BatchConsumption>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp, nameof(BatchComputeAsync));
    }

    public async ValueTask<ImmutableArray<Avatar>> GetAvatarsAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        int currentPage = 1;
        SyncAvatarFilter filter = new() { Uid = userAndUid.Uid.Value, Region = userAndUid.Uid.Region };

        ImmutableArray<Avatar>.Builder avatars = ImmutableArray.CreateBuilder<Avatar>();
        Response<ListWrapper<Avatar>>? resp;

        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(userAndUid.IsOversea);

        do
        {
            filter.Page = currentPage++;

            HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
                .SetRequestUri(apiEndpoints.CalculateSyncAvatarList())
                .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
                .SetReferer(userAndUid.IsOversea ? apiEndpoints.ActHoyolabReferer() : apiEndpoints.WebStaticReferer())
                .PostJson(filter);

            resp = await builder
                .SendAsync<Response<ListWrapper<Avatar>>>(httpClient, token)
                .ConfigureAwait(false);

            Response.Response.DefaultIfNull(ref resp);

            if (ResponseValidator.TryValidate(resp, serviceProvider, out ListWrapper<Avatar>? listWrapper))
            {
                avatars.AddRange(listWrapper.List);
            }
            else
            {
                // Hot path to exit loop
                break;
            }

            await Task.Delay(Random.Shared.Next(0, 1000), token).ConfigureAwait(false);
        }
        while (resp.Data is { List.Length: 20 });

        return avatars.ToImmutable();
    }

    public async ValueTask<Response<AvatarDetail>> GetAvatarDetailAsync(UserAndUid userAndUid, Avatar avatar, CancellationToken token = default)
    {
        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(userAndUid.IsOversea);

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CalculateSyncAvatarDetail(avatar.Id, userAndUid.Uid.Value))
            .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
            .SetReferer(userAndUid.IsOversea ? apiEndpoints.ActHoyolabReferer() : apiEndpoints.WebStaticReferer())
            .Get();

        Response<AvatarDetail>? resp = await builder
            .SendAsync<Response<AvatarDetail>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<FurnitureListWrapper>> FurnitureBlueprintAsync(Model.Entity.User user, string shareCode, CancellationToken token)
    {
        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(user.IsOversea);

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CalculateFurnitureBlueprint(shareCode))
            .SetUserCookieAndFpHeader(user, CookieType.CookieToken)
            .SetReferer(user.IsOversea ? apiEndpoints.ActHoyolabReferer() : apiEndpoints.WebStaticReferer())
            .Get();

        Response<FurnitureListWrapper>? resp = await builder
            .SendAsync<Response<FurnitureListWrapper>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<Response<ListWrapper<Item>>> FurnitureComputeAsync(Model.Entity.User user, ImmutableArray<Item> items, CancellationToken token)
    {
        ListWrapper<IdCount> data = new() { List = items.SelectAsArray(static i => new IdCount { Id = i.Id, Count = i.Num }) };

        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(user.IsOversea);
        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CalculateFurnitureCompute())
            .SetUserCookieAndFpHeader(user, CookieType.CookieToken)
            .SetReferer(user.IsOversea ? apiEndpoints.ActHoyolabReferer() : apiEndpoints.WebStaticReferer())
            .PostJson(data);

        Response<ListWrapper<Item>>? resp = await builder
            .SendAsync<Response<ListWrapper<Item>>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp);
    }

    public async ValueTask<ImmutableArray<Avatar>> GetAllAvatarsAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        SyncAvatarFilter filter = new() { Page = 1, Size = 1000, IsAll = true };

        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(userAndUid.IsOversea);

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CalculateAvatarList())
            .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
            .SetReferer(userAndUid.IsOversea ? apiEndpoints.ActHoyolabReferer() : apiEndpoints.WebStaticReferer())
            .PostJson(filter);

        Response<ListWrapper<Avatar>>? resp = await builder
            .SendAsync<Response<ListWrapper<Avatar>>>(httpClient, token)
            .ConfigureAwait(false);

        Response.Response.DefaultIfNull(ref resp);

        if (ResponseValidator.TryValidate(resp, serviceProvider, out ListWrapper<Avatar>? listWrapper))
        {
            return listWrapper.List;
        }

        return default;
    }

    public async ValueTask<ImmutableArray<Weapon>> GetAllWeaponsAsync(UserAndUid userAndUid, CancellationToken token = default)
    {
        SyncWeaponFilter filter = new() { WeaponLevels = [4, 5] };

        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(userAndUid.IsOversea);

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CalculateWeaponList())
            .SetUserCookieAndFpHeader(userAndUid, CookieType.CookieToken)
            .SetReferer(userAndUid.IsOversea ? apiEndpoints.ActHoyolabReferer() : apiEndpoints.WebStaticReferer())
            .PostJson(filter);

        Response<ListWrapper<Weapon>>? resp = await builder
            .SendAsync<Response<ListWrapper<Weapon>>>(httpClient, token)
            .ConfigureAwait(false);

        Response.Response.DefaultIfNull(ref resp);

        if (ResponseValidator.TryValidate(resp, serviceProvider, out ListWrapper<Weapon>? listWrapper))
        {
            return listWrapper.List;
        }

        return default;
    }

    private class SyncAvatarFilter
    {
        [JsonPropertyName("element_attr_ids")]
        public List<int>? ElementAttrIds { get; set; } = [];

        [JsonPropertyName("weapon_cat_ids")]
        public List<int>? WeaponCatIds { get; set; } = [];

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; } = 20;

        [JsonPropertyName("is_all")]
        public bool? IsAll { get; set; }

        [JsonPropertyName("uid")]
        public string? Uid { get; set; }

        [JsonPropertyName("region")]
        public Region? Region { get; set; }
    }

    private class SyncWeaponFilter
    {
        [JsonPropertyName("weapon_cat_ids")]
        public List<int>? WeaponCatIds { get; set; } = [];

        [JsonPropertyName("weapon_levels")]
        public List<int>? WeaponLevels { get; set; } = [];

        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        [JsonPropertyName("size")]
        public int Size { get; set; } = 1000;
    }

    private class IdCount
    {
        [JsonPropertyName("cnt")]
        public uint Count { get; set; }

        [JsonPropertyName("id")]
        public uint Id { get; set; }
    }

    private class BatchConsumptionData
    {
        [JsonPropertyName("items")]
        public required ImmutableArray<AvatarPromotionDelta> Items { get; init; }

        [JsonPropertyName("region")]
        public required Region Region { get; init; }

        [JsonPropertyName("uid")]
        public required string Uid { get; init; }
    }
}