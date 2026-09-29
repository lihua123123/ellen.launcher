// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.EnemyArchive;
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
}
