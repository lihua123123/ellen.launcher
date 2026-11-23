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
    /* 50 条一批是实测能通过的最大量级（响应约 4.8 MB）。
       再往上单次响应会到十几 MB，没必要；分得太小又会低估「持有量」——
       因为 overall_consume 的 num - lack_num 实际是 min(持有, 该批需求)。 */
    private const int BatchComputeChunkSize = 50;

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
        if (response.ReturnCode is 0)
        {
            return response;
        }

        if (deltas.Length <= 1)
        {
            if (deltas.IsEmpty)
            {
                return response;
            }

            /* 单条仍然失败：几乎都是 skill_list 里的技能分组 id 服务端不认识。
               典型是旅行者 10000005 —— 元数据里用的是基础形态的分组 id（830 / 832），
               而养成计算器只认各元素形态的 id，于是整条请求直接 -500001。
               去掉 skill_list 再试一次：角色本体、武器、元素宝石这些材料照样算得出来，
               只是少算天赋书，总比整次同步失败强。 */

            AvatarPromotionDelta stripped = CloneWithoutSkills(deltas[0]);
            return await SendBatchComputeAsync(userAndUid, [stripped], syncInventory, token).ConfigureAwait(false);
        }

        await Task.Delay(300, token).ConfigureAwait(false);

        int leftCount = deltas.Length / 2;
        ImmutableArray<AvatarPromotionDelta> left = Slice(deltas, 0, leftCount);
        ImmutableArray<AvatarPromotionDelta> right = Slice(deltas, leftCount, deltas.Length - leftCount);

        // 逐层递归二分，而不是只拆一层：服务端上限可能低到几条，
        // 拆一层之后仍然可能失败，那样整次同步就白跑了。
        Response<BatchConsumption> leftResponse = await ComputeChunkWithRetryAsync(userAndUid, left, syncInventory, token).ConfigureAwait(false);
        if (leftResponse.ReturnCode is not 0)
        {
            return leftResponse;
        }

        Response<BatchConsumption> rightResponse = await ComputeChunkWithRetryAsync(userAndUid, right, syncInventory, token).ConfigureAwait(false);
        if (rightResponse.ReturnCode is not 0)
        {
            return rightResponse;
        }

        return MergeResponses([leftResponse, rightResponse]);
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

    /// <summary>
    /// 复制一条 delta 但丢掉 skill_list，用于「服务端不认这组技能分组 id」时降级重试。
    /// SkillList 保持 default，序列化时会因为 <c>WhenWritingDefault</c> 被整个跳过。
    /// </summary>
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

        [JsonPropertyName("region")]
        public required Region Region { get; init; }

        [JsonPropertyName("uid")]
        public required string Uid { get; init; }
    }
}