// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

namespace Ellen.Launcher.Service.Metadata;

/// <summary>
/// 从自建数据站（ellen.wiki）同步元数据快照。
///
/// 为什么不用 Snap.Metadata：那份仓库的更新节奏跟不上游戏版本，而且拿到授权很困难。
/// 本站的 <c>/api/launcher/*</c> 由 AnimeGameData 原始配置表直接生成成 Snap.Metadata
/// 兼容格式，结构化字段完全一致，所以 Launcher 侧的模型与反序列化代码无需改动。
/// </summary>
public interface IWikiMetadataSyncService
{
    /// <summary>
    /// 已同步快照的根目录（<c>.../Metadata/Wiki/Genshin/&lt;locale&gt;</c>）。
    /// 未同步时目录可能不存在。
    /// </summary>
    string SyncedLocalizedDataFolder { get; }

    /// <summary>
    /// 已同步快照是否可直接读取（以 <c>Meta.json</c> 哨兵文件为准，与内置快照口径一致）。
    /// </summary>
    bool HasSyncedData { get; }

    /// <summary>
    /// 当前使用的数据站地址（可在设置里用 <c>Ellen::Metadata::Wiki::Endpoint</c> 覆盖）。
    /// </summary>
    string Endpoint { get; }

    /// <summary>
    /// 后台同步：不阻塞调用方，失败只记日志。带节流，短时间重复调用不会重复请求。
    /// </summary>
    void SyncInBackground();

    /// <summary>
    /// 立即同步。返回 <see langword="true"/> 表示本地快照被更新（首次下载或内容变化）。
    /// </summary>
    /// <param name="force">忽略 digest 比对，强制重新下载。</param>
    /// <param name="token">取消令牌。</param>
    ValueTask<bool> SyncAsync(bool force, CancellationToken token);
}
