// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.ZZZ;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 绝区零调频元数据：运行期从公开数据源刷新并缓存，随包快照作为离线兜底。
/// </summary>
public interface IZZZGachaMetadataService
{
    /// <summary>
    /// 快照刷新完成（包括后台补卡池图）时触发；可能来自任意线程。
    /// </summary>
    event EventHandler? Updated;

    /// <summary>
    /// 当前可用的卡池档期列表。
    /// </summary>
    ImmutableArray<ZZZGachaBanner> Banners { get; }

    /// <summary>
    /// 当前快照的刷新时间；随包兜底时为 <see cref="DateTimeOffset.MinValue"/>。
    /// </summary>
    DateTimeOffset LastUpdated { get; }

    /// <summary>
    /// 按物品 id 获取图标 / 名称 / 稀有度，未收录时返回 <see langword="null"/>。
    /// </summary>
    ZZZGachaItemInfo? GetById(uint itemId);

    /// <summary>
    /// 确保本地快照已加载；数据过期时尝试刷新一次（失败静默回退当前快照）。
    /// </summary>
    ValueTask EnsureInitializedAsync(CancellationToken token = default);
}
