// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.EllenWiki;

/// <summary>
/// 从 ellen.wiki 的公开数据 API 同步数据快照到本地数据目录。
/// </summary>
public interface IEllenWikiDataService
{
    /// <summary>
    /// 「元素附着及产球」快照更新后触发。
    /// </summary>
    event EventHandler? AttachmentUpdated;

    /// <summary>
    /// 返回运行期同步得到的「元素附着及产球」快照路径；尚未同步时返回 <see langword="null"/>，
    /// 调用方应回退到随包快照。
    /// </summary>
    string? GetAttachmentSnapshotPath();

    /// <summary>
    /// 同步全部数据集。启动时在后台调用。
    /// </summary>
    ValueTask InitializeAsync(CancellationToken token = default);
}
