// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Metadata.ZZZ;

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 绝区零调频物品元数据查询（随包快照，运行期不发起网络请求）。
/// </summary>
public interface IZZZGachaItemMetadataService
{
    /// <summary>
    /// 按物品 id 获取图标 / 名称 / 稀有度，未收录时返回 <see langword="null"/>。
    /// </summary>
    ZZZGachaItemInfo? GetById(uint itemId);
}
