// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.EnemyArchive;
using System.Collections.Immutable;

namespace Ellen.Launcher.Service.EnemyArchive;

/// <summary>
/// 敌人图鉴数据源：把上游 GIEnemyChartXML 的 722 条敌人原样呈现给图鉴页。
/// </summary>
public interface IMonsterCatalogService
{
    /// <summary>
    /// 读取 <c>Resource/EnemyArchives/MonsterCatalog.json</c>；读取失败时返回空集合（页面显示为空，不崩）。
    /// </summary>
    ValueTask<ImmutableArray<MonsterCatalogEntry>> GetEntriesAsync();
}
