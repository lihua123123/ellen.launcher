// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using Ellen.Launcher.Core.Database.Abstraction;
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

    public static GachaArchive Create(string uid)
    {
        return new() { Uid = uid };
    }
}