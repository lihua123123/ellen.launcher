// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.HardChallenge;
using System.Collections.Immutable;

namespace Ellen.Launcher.ViewModel.HardChallenge;

public sealed class MonsterView
{
    private MonsterView(HardChallengeMonster monster, HardChallengeMetadataContext context)
    {
        Name = monster.Name;
        Level = LevelFormat.Format(monster.Level);
        Icon = monster.Icon;
        Descriptions = [.. monster.Descriptions.Where(static d => !string.IsNullOrEmpty(d))];
        Tags = monster.Tags;
    }

    public string Name { get; }

    public string Level { get; }

    public Uri Icon { get; }

    public ImmutableArray<string> Descriptions { get; }

    public ImmutableArray<HardChallengeMonsterTag> Tags { get; }

    public static MonsterView Create(HardChallengeMonster monster, HardChallengeMetadataContext context)
    {
        return new(monster, context);
    }
}