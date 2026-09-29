// Copyright (c) DGP Studio. All rights reserved.
// Licensed under the MIT license.

using CommunityToolkit.Mvvm.ComponentModel;
using Ellen.Launcher.Core.Database.Abstraction;
using Ellen.Launcher.Model.Entity.Abstraction;
using Ellen.Launcher.Model.Entity.Primitive;
using Ellen.Launcher.UI.Xaml.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ellen.Launcher.Model.Entity;

[Table("game_accounts")]
public sealed partial class GameAccount : ObservableObject,
    IAppDbEntity,
    IReorderable,
    IPropertyValuesProvider
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid InnerId { get; set; }

    public SchemeType Type { get; set; }

    public string Name { get; set; } = default!;

    /// <summary>
    /// [MIHOYOSDK_ADL_PROD_CN_h3123967166]
    /// [MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810]
    /// </summary>
    public string MihoyoSDK { get; set; } = default!;

    public string? Mid { get; set; }

    public string? MacAddress { get; set; }

    public int Index { get; set; }

    [NotMapped]
    public bool IsExpired { get; set; }

    public static GameAccount Create(SchemeType type, string name, string sdk, string? mid, string? mac)
    {
        return new()
        {
            Type = type,
            Name = name,
            MihoyoSDK = sdk,
            MacAddress = mac,
            Mid = mid,
        };
    }

    public void UpdateName(string name)
    {
        Name = name;
        OnPropertyChanged(nameof(Name));
    }
}