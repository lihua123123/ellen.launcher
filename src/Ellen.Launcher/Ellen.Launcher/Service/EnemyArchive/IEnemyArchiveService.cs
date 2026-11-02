// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.EnemyArchive;
using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.EnemyArchive;

/// <summary>
/// 提供随应用发布的敌人档案（抗性 / 技能 / 机制）。
/// </summary>
public interface IEnemyArchiveService
{
    /// <summary>
    /// 获取全部档案条目，首次调用后结果会被缓存。
    /// </summary>
    ValueTask<ImmutableArray<EnemyArchiveEntry>> GetEntriesAsync();

    /// <summary>
    /// 获取「元数据怪物名 → GitHub 目录表 IconPath 图标名」的覆盖表。
    /// 用于修正元数据里母体/子体共用一张头像的问题。
    /// </summary>
    ValueTask<FrozenDictionary<string, string>> GetMonsterIconMapAsync();

    /// <summary>
    /// 获取静态服务器上取不到图的图标名（<c>MonsterIconUnavailable.json</c>）。
    /// 这些怪物在图鉴里只显示文字，不再显示破图 / 空图。
    /// </summary>
    ValueTask<FrozenSet<string>> GetUnavailableIconNamesAsync();
}
