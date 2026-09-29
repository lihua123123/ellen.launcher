// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Metadata.Converter;
using Ellen.Launcher.Model.Metadata.Tower;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Web.Endpoint.Upstream;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.SpiralAbyss;

public sealed class MonsterView : INameIcon<Uri>
{
    private MonsterView(in MonsterDescribeId id)
    {
        Name = $"Unknown {id}";
        Icon = StaticResourcesEndpoints.UIIconNone;
        Affixes = [];
        Count = 1;
    }

    private MonsterView(TowerMonster towerMonster, Model.Metadata.Monster.Monster metaMonster)
    {
        Name = metaMonster.Name ?? $"Unknown {towerMonster.Id}";
        Icon = MonsterIconConverter.IconNameToUri(metaMonster.Icon);
        Affixes = towerMonster.Affixes.EmptyIfDefault();
        Count = (int)towerMonster.Count;
        AttackMonolith = towerMonster.AttackMonolith;
    }

    public string Name { get; }

    public Uri Icon { get; }

    public ImmutableArray<NameDescription> Affixes { get; }

    public int Count { get; }

    public bool IsCountOne { get => Count == 1; }

    public bool AttackMonolith { get; }

    public static MonsterView Default(in MonsterDescribeId id)
    {
        return new(id);
    }

    public static MonsterView Create(TowerMonster tower, Model.Metadata.Monster.Monster meta)
    {
        return new(tower, meta);
    }
}