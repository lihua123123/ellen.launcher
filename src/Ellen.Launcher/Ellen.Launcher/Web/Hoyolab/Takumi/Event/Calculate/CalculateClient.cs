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
using System.Text;

namespace Ellen.Launcher.Web.Hoyolab.Takumi.Event.Calculate;

[HttpClient(HttpClientConfiguration.Default)]
public sealed partial class CalculateClient
{
    private const int BatchComputeChunkSize = 30;
    private const string ChineseCalculatorReferer = "https://act.mihoyo.com/";

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
        if (deltas.IsEmpty)
        {
            return new Response<BatchConsumption>(-1, SH.ServiceInventoryCalculatorNoDeltas, default);
        }

        if (deltas.Length <= BatchComputeChunkSize)
        {
            return await SendBatchComputeAsync(userAndUid, deltas, syncInventory, token).ConfigureAwait(false);
        }

        List<Response<BatchConsumption>> responses = [];
        for (int offset = 0; offset < deltas.Length; offset += BatchComputeChunkSize)
        {
            int count = Math.Min(BatchComputeChunkSize, deltas.Length - offset);
            ImmutableArray<AvatarPromotionDelta> chunk = Slice(deltas, offset, count);

            Response<BatchConsumption> response = await ComputeChunkWithRetryAsync(
                userAndUid,
                chunk,
                syncInventory,
                token).ConfigureAwait(false);

            if (response.ReturnCode is not 0)
            {
                return response;
            }

            responses.Add(response);
        }

        return MergeResponses(responses);
    }

    private async ValueTask<Response<BatchConsumption>> ComputeChunkWithRetryAsync(
        UserAndUid userAndUid,
        ImmutableArray<AvatarPromotionDelta> deltas,
        bool syncInventory,
        CancellationToken token)
    {
        Response<BatchConsumption> response = await SendBatchComputeAsync(userAndUid, deltas, syncInventory, token).ConfigureAwait(false);
        if (response.ReturnCode is 0 || deltas.Length <= 1)
        {
            return response;
        }

        await Task.Delay(300, token).ConfigureAwait(false);

        int leftCount = deltas.Length / 2;
        ImmutableArray<AvatarPromotionDelta> left = Slice(deltas, 0, leftCount);
        ImmutableArray<AvatarPromotionDelta> right = Slice(deltas, leftCount, deltas.Length - leftCount);

        Response<BatchConsumption> leftResponse = await SendBatchComputeAsync(userAndUid, left, syncInventory, token).ConfigureAwait(false);
        Response<BatchConsumption> rightResponse = await SendBatchComputeAsync(userAndUid, right, syncInventory, token).ConfigureAwait(false);

        if (leftResponse.ReturnCode is 0 && rightResponse.ReturnCode is 0)
        {
            return MergeResponses([leftResponse, rightResponse]);
        }

        return leftResponse.ReturnCode is not 0 ? leftResponse : rightResponse;
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
            Lang = "zh-cn",
            Region = syncInventory ? userAndUid.Uid.Region : default!,
            Uid = syncInventory ? userAndUid.Uid.ToString() : default!,
        };

        IApiEndpoints apiEndpoints = apiEndpointsFactory.Create(userAndUid.IsOversea);

        HttpRequestMessageBuilder builder = httpRequestMessageBuilderFactory.Create()
            .SetRequestUri(apiEndpoints.CalculateBatchCompute())
            .SetUserCookieAndFpHeader(userAndUid.User, CookieType.Cookie)
            .SetReferer(userAndUid.IsOversea ? apiEndpoints.ActHoyolabReferer() : ChineseCalculatorReferer)
            .SetHeader("x-rpc-device_id", HoyolabOptions.DeviceId36)
            .SetHeader("x-rpc-platform", "4")
            .SetHeader("x-rpc-lrsag", string.Empty)
            .SetHeader("x-rpc-page", "__#")
            .SetHeader("x-rpc-cal_type", "0")
            .SetHeader("x-rpc-stat_platform", "PC")
            .PostJson(data, Encoding.UTF8);

        Response<BatchConsumption>? resp = await builder
            .SendAsync<Response<BatchConsumption>>(httpClient, token)
            .ConfigureAwait(false);

        return Response.Response.DefaultIfNull(resp, nameof(BatchComputeAsync));
    }

    private static Response<BatchConsumption> MergeResponses(IEnumerable<Response<BatchConsumption>> responses)
    {
        List<Consumption> items = [];
        Dictionary<uint, Item> overallConsume = [];
        Dictionary<uint, Item> availableMaterial = [];
        bool hasUserInfo = false;

        foreach (Response<BatchConsumption> response in responses)
        {
            if (response.Data is not { } data)
            {
                continue;
            }

            hasUserInfo |= data.HasUserInfo;

            if (!data.Items.IsDefault)
            {
                items.AddRange(data.Items);
            }

            MergeItems(overallConsume, data.OverallConsume);
            MergeItems(availableMaterial, data.AvailableMaterial);
        }

        BatchConsumption merged = new()
        {
            HasUserInfo = hasUserInfo,
        };

        if (items.Count > 0)
        {
            merged.Items = [.. items];
        }

        if (availableMaterial.Count > 0)
        {
            merged.AvailableMaterial = [.. availableMaterial.Values];
        }

        if (overallConsume.Count > 0)
        {
            merged.OverallConsume = [.. overallConsume.Values];
        }

        return new Response<BatchConsumption>(0, "OK", merged);
    }

    private static void MergeItems(Dictionary<uint, Item> target, ImmutableArray<Item> source)
    {
        if (source.IsDefault)
        {
            return;
        }

        foreach (Item item in source)
        {
            if (!target.TryGetValue(item.Id, out Item? existing) || GetOwnedCount(item) > GetOwnedCount(existing))
            {
                target[item.Id] = item;
            }
        }
    }

    private static int GetOwnedCount(Item item)
    {
        return (int)item.Num - item.LackNum;
    }

    private static ImmutableArray<AvatarPromotionDelta> Slice(ImmutableArray<AvatarPromotionDelta> source, int start, int length)
    {
        ImmutableArray<AvatarPromotionDelta>.Builder builder = ImmutableArray.CreateBuilder<AvatarPromotionDelta>(length);
        for (int i = start; i < start + length; i++)
        {
            builder.Add(source[i]);
        }

        return builder.ToImmutable();
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

        return [];
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

        return [];
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

        [JsonPropertyName("lang")]
        public string Lang { get; init; } = "zh-cn";

        [JsonPropertyName("region")]
        public required Region Region { get; init; }

        [JsonPropertyName("uid")]
        public required string Uid { get; init; }
    }
}