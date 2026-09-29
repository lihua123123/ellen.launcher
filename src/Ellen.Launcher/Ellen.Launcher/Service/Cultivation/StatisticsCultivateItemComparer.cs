// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Collection.Generic;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.ViewModel.Cultivation;

namespace Ellen.Launcher.Service.Cultivation;

public sealed class StatisticsCultivateItemComparer : DelegatingPropertyComparer<StatisticsCultivateItem, MaterialId>
{
    private static readonly LazySlim<StatisticsCultivateItemComparer> LazyShared = new(() => new());

    private StatisticsCultivateItemComparer()
        : base(static i => i.Inner.Id, MaterialIdComparer.Shared)
    {
    }

    public static StatisticsCultivateItemComparer Shared { get => LazyShared.Value; }
}