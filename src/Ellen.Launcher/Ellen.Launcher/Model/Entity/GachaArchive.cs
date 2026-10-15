// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database.Abstraction;
using Ellen.Launcher.Model.Intrinsic;
using Ellen.Launcher.UI.Xaml.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ellen.Launcher.Model.Entity;

[Table("gacha_archives")]
public sealed partial class GachaArchive : ISelectable, IPropertyValuesProvider
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid InnerId { get; set; }

    public string Uid { get; set; } = default!;

    public bool IsSelected { get; set; }

    /// <summary>
    /// 存档所属的游戏，存量数据默认为原神
    /// </summary>
    public GachaGameType Game { get; set; }

    public static GachaArchive Create(string uid, GachaGameType game = GachaGameType.Genshin)
    {
        return new() { Uid = uid, Game = game };
    }
}