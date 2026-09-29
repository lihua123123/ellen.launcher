// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Model;
using Ellen.Launcher.Model.Primitive;
using Ellen.Launcher.Web.Hoyolab.Takumi.GameRecord.RoleCombat;

namespace Ellen.Launcher.ViewModel.RoleCombat;

public sealed class EnemyView : INameIcon<Uri>
{
    private EnemyView(RoleCombatEnemy roleCombatEnemy)
    {
        Name = roleCombatEnemy.Name;
        Icon = roleCombatEnemy.Icon.ToUri();
        Level = LevelFormat.Format(roleCombatEnemy.Level);
    }

    public string Name { get; }

    public Uri Icon { get; }

    public string Level { get; }

    public static EnemyView Create(RoleCombatEnemy roleCombatEnemy)
    {
        return new(roleCombatEnemy);
    }
}